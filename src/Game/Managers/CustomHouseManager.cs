﻿using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.IO;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using ClassicUO.Utility.Collections;

namespace ClassicUO.Game.Managers
{
    struct CustomBuildObject
    {
        public CustomBuildObject(ushort graphic)
        {
            Graphic = graphic;
            X = Y = Z = 0;
        }

        public ushort Graphic;
        public int X, Y, Z;
    }

    class CustomHouseManager
    {
        static CustomHouseManager()
        {
            ParseFileWithCategory<CustomHouseWall, CustomHouseWallCategory>(Walls, Path.Combine(FileManager.UoFolderPath, "walls.txt"));
            ParseFile(Floors, Path.Combine(FileManager.UoFolderPath, "floors.txt"));
            ParseFile(Doors, Path.Combine(FileManager.UoFolderPath, "doors.txt"));
            ParseFileWithCategory<CustomHouseMisc, CustomHouseMiscCategory>(Miscs, Path.Combine(FileManager.UoFolderPath, "misc.txt"));
            ParseFile(Stairs, Path.Combine(FileManager.UoFolderPath, "stairs.txt"));
            ParseFile(Teleports, Path.Combine(FileManager.UoFolderPath, "teleprts.txt"));
            ParseFileWithCategory<CustomHouseRoof, CustomHouseRoofCategory>(Roofs, Path.Combine(FileManager.UoFolderPath, "roof.txt"));
            ParseFile(ObjectsInfo, Path.Combine(FileManager.UoFolderPath, "suppinfo.txt"));
        }

        public static readonly List<CustomHouseWallCategory> Walls = new List<CustomHouseWallCategory>();
        public static readonly List<CustomHouseFloor> Floors = new List<CustomHouseFloor>();
        public static readonly List<CustomHouseDoor> Doors = new List<CustomHouseDoor>();
        public static readonly List<CustomHouseMiscCategory> Miscs = new List<CustomHouseMiscCategory>();
        public static readonly List<CustomHouseStair> Stairs = new List<CustomHouseStair>();
        public static readonly List<CustomHouseTeleport> Teleports = new List<CustomHouseTeleport>();
        public static readonly List<CustomHouseRoofCategory> Roofs = new List<CustomHouseRoofCategory>();
        public static readonly List<CustomHousePlaceInfo> ObjectsInfo = new List<CustomHousePlaceInfo>();


        public readonly int[] FloorVisionState = new int[4];

        public int Category = -1,
                 MaxPage = 1,
                 CurrentFloor = 1,
                 FloorCount = 4,
                 RoofZ = 1,
                 MinHouseZ = -120,
                 Components,
                 Fixtures,
                 MaxComponets,
                 MaxFixtures;


        public ushort SelectedGraphic;
        public bool Erasing, SeekTile, ShowWindow, CombinedStair;
        public Point StartPos, EndPos;
        public CUSTOM_HOUSE_GUMP_STATE State = CUSTOM_HOUSE_GUMP_STATE.CHGS_WALL;

        public readonly Serial Serial;

        public CustomHouseManager(Serial serial)
        {
            Serial = serial;

            InitializeHouse();
        }

        private void InitializeHouse()
        {
            Item foundation = World.Items.Get(Serial);

            if (foundation != null)
            {
                MinHouseZ = foundation.Z + 7;

                MultiInfo multi = foundation.MultiInfo;

                if (multi != null)
                {
                    StartPos.X = foundation.X + multi.MinX;
                    StartPos.Y = foundation.Y + multi.MinY;
                    EndPos.X = foundation.X + multi.MaxX + 1;
                    EndPos.Y = foundation.Y + multi.MaxY + 1;
                }

                int width = Math.Abs(EndPos.X - StartPos.X);
                int height = Math.Abs(EndPos.Y - StartPos.Y);

                if (width > 13 || height > 13)
                {
                    FloorCount = 4;
                }
                else
                {
                    FloorCount = 3;
                }

                int componentsOnFloor = (width - 1) * (height - 1);

                MaxComponets = FloorCount * (componentsOnFloor + 2 * (width + height) - 4) -
                               (int)((double)(FloorCount * componentsOnFloor) * -0.25) + 2 * width + 3 * height - 5;
                MaxFixtures = MaxComponets / 20;
            }
        }

        public void GenerateFloorPlace()
        {
            Item foundationItem = World.Items.Get(Serial);

            if (foundationItem != null && World.HouseManager.TryGetHouse(Serial, out var house))
            {
                house.ClearCustomHouseComponents(CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL);

                foreach (Multi item in house.Components)
                {
                    if (!item.IsCustom)
                        continue;

                    int currentFloor = -1;
                    int floorZ = foundationItem.Z + 7;
                    int itemZ = item.Z;

                    bool ignore = false;

                    for (int i = 0; i < 4; i++)
                    {
                        int offset = 0/*i != 0 ? 0 : 7*/;

                        if (itemZ >= floorZ - offset && itemZ < floorZ + 20)
                        {
                            currentFloor = i;
                            break;
                        }

                        floorZ += 20;
                    }

                    if (currentFloor == -1)
                    {
                        ignore = true;
                        currentFloor = 0;
                        //continue;
                    }

                    (int floorCheck1, int floorCheck2) = SeekGraphicInCustomHouseObjectList(Floors, item.Graphic);

                    var state = item.State;

                    if (floorCheck1 != -1 && floorCheck2 != -1)
                    {
                        state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR;

                        if (FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_HIDE_FLOOR)
                        {
                            state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_IGNORE_IN_RENDER;
                        }
                        else if (FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_TRANSPARENT_FLOOR ||
                                 FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_TRANSLUCENT_FLOOR)
                        {
                            state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_TRANSPARENT;
                        }
                    }
                    else
                    {
                        (int stairCheck1, int stairCheck2) = SeekGraphicInCustomHouseObjectList(Stairs, item.Graphic);

                        if (stairCheck1 != -1 && stairCheck2 != -1)
                        {
                            state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR;
                        }
                        else
                        {
                            (int roofCheck1, int roofCheck2) = SeekGraphicInCustomHouseObjectListWithCategory<CustomHouseRoof, CustomHouseRoofCategory>(Roofs, item.Graphic);

                            if (roofCheck1 != -1 && roofCheck2 != -1)
                            {
                                state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF;
                            }
                            else
                            {
                                (int fixtureCheck1, int fixtureCheck2) = SeekGraphicInCustomHouseObjectList(Doors, item.Graphic);

                                if (fixtureCheck1 == -1 || fixtureCheck2 == -1)
                                {
                                    (fixtureCheck1, fixtureCheck2) = SeekGraphicInCustomHouseObjectList(Teleports, item.Graphic);
                                }

                                if (fixtureCheck1 != -1 && fixtureCheck2 != -1)
                                {
                                    state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FIXTURE;
                                }
                            }
                        }

                        if (!ignore)
                        {
                            if (FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_HIDE_CONTENT)
                            {
                                state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_IGNORE_IN_RENDER;
                            }
                            else if (FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_TRANSPARENT_CONTENT)
                            {
                                state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_TRANSPARENT;
                            }
                        }

                    }

                    if (!ignore)
                    {
                        if (FloorVisionState[currentFloor] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_HIDE_ALL)
                        {
                            state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_IGNORE_IN_RENDER;
                        }
                    }

                    item.State = state;
                }

                int z = foundationItem.Z + 7;

                for (int x = StartPos.X + 1; x < EndPos.X; x++)
                {
                    for (int y = StartPos.Y + 1; y < EndPos.Y; y++)
                    {
                        var multi = house.Components.Where(s => s.X == x && s.Y == y);

                        if (multi == null)
                            continue;

                        Multi floorMulti = null;
                        Multi floorCustomMulti = null;

                        foreach (Multi item in multi)
                        {
                            if (item.Z != z || (item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) == 0)
                            {
                                continue;
                            }

                            if (item.IsCustom)
                            {
                                floorCustomMulti = item;
                            }
                            else
                            {
                                floorMulti = item;
                            }
                        }

                        if (floorMulti != null && floorCustomMulti == null)
                        {
                            var mo = house.Add(floorMulti.Graphic, 0, x - foundationItem.X, y - foundationItem.Y, (sbyte)z, true);
                            mo.AlphaHue = 0xFF;

                            CUSTOM_HOUSE_MULTI_OBJECT_FLAGS state = CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR;

                            if (FloorVisionState[0] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_HIDE_FLOOR)
                            {
                                state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_IGNORE_IN_RENDER;
                            }
                            else if (FloorVisionState[0] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_TRANSPARENT_FLOOR ||
                                     FloorVisionState[0] == (int)CUSTOM_HOUSE_FLOOR_VISION_STATE.CHGVS_TRANSLUCENT_FLOOR)
                            {
                                state |= CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_TRANSPARENT;
                            }

                            mo.State = state;
                        }
                    }
                }

                for (int i = 0; i < FloorCount; i++)
                {
                    int minZ = foundationItem.Z + 7 + i * 20;
                    int maxZ = minZ + 20;

                    for (int j = 0; j < 2; j++)
                    {
                        List<Point> validatedFloors = new List<Point>();

                        for (int x = StartPos.X; x < EndPos.X + 1; x++)
                        {
                            for (int y = StartPos.Y; y < EndPos.Y + 1; y++)
                            {
                                var multi = house.GetMultiAt(x, y);

                                if (multi == null)
                                    continue;

                                foreach (Multi item in house.Components)
                                {
                                    if (!item.IsCustom)
                                        continue;

                                    if (j == 0)
                                    {
                                        if (i == 0 && item.Z < minZ)
                                        {
                                            item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE;
                                            continue;
                                        }

                                        if (((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) == 0))
                                        {
                                            continue;
                                        }

                                        if (i == 0 && item.Z >= minZ && item.Z < maxZ)
                                        {
                                            item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE;
                                            continue;
                                        }
                                    }

                                    if (((item.State & (CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL)) == 0) && item.Z >= minZ && item.Z < maxZ)
                                    {
                                        if (!ValidateItemPlace(foundationItem, item, minZ, maxZ, validatedFloors))
                                        {
                                            item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE;
                                        }
                                        else
                                        {
                                            item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE;
                                        }
                                    }
                                }
                            }
                        }

                        if (i != 0 && j == 0)
                        {
                            foreach (Point point in validatedFloors)
                            {
                                var multi = house.GetMultiAt(point.X, point.Y);

                                if (multi == null)
                                    continue;

                                foreach (Multi item in house.Components)
                                {
                                    if (item.IsCustom && (((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) && item.Z >= minZ && item.Z < maxZ))
                                    {
                                        item.State = item.State & ~CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE;
                                    }
                                }
                            }

                            for (int x = StartPos.X; x < EndPos.X + 1; x++)
                            {
                                int minY = 0, maxY = 0;

                                for (int y = StartPos.Y; y < EndPos.Y + 1; y++)
                                {
                                    var multi = house.GetMultiAt(x, y);

                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            minY = y;
                                            break;
                                        }
                                    }

                                    if (minY != 0)
                                    {
                                        break;
                                    }
                                }

                                for (int y = EndPos.Y; y >= StartPos.Y; y--)
                                {
                                    var multi = house.GetMultiAt(x, y);
                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            maxY = y;
                                            break;
                                        }
                                    }

                                    if (maxY != 0)
                                    {
                                        break;
                                    }
                                }

                                for (int y = minY; y < maxY; y++)
                                {
                                    var multi = house.GetMultiAt(x, y);
                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            item.State = item.State & ~CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE;
                                        }
                                    }
                                }
                            }

                            for (int y = StartPos.Y; y < EndPos.Y + 1; y++)
                            {
                                int minX = 0;
                                int maxX = 0;

                                for (int x = StartPos.X; x < EndPos.X + 1; x++)
                                {
                                    var multi = house.GetMultiAt(x, y);
                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            minX = x;
                                            break;
                                        }
                                    }

                                    if (minX != 0)
                                    {
                                        break;
                                    }
                                }

                                for (int x = EndPos.X; x >= StartPos.X; x--)
                                {
                                    var multi = house.GetMultiAt(x, y);
                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            maxX = x;
                                            break;
                                        }
                                    }

                                    if (maxX != 0)
                                    {
                                        break;
                                    }
                                }

                                for (int x = minX; x < maxX; x++)
                                {
                                    var multi = house.GetMultiAt(x, y);
                                    if (multi == null)
                                        continue;

                                    foreach (Multi item in house.Components)
                                    {
                                        if (item.IsCustom && ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0) &&
                                            ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0) &&
                                            item.Z >= minZ && item.Z < maxZ)
                                        {
                                            item.State = item.State & ~CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                z = foundationItem.Z + 7 + 20;

                ushort color = 0x0051;

                for (int i = 1; i < CurrentFloor; i++)
                {
                    for (int x = StartPos.X; x < EndPos.X; x++)
                    {
                        for (int y = StartPos.Y; y < EndPos.Y; y++)
                        {
                            ushort tempColor = color;

                            if (x == StartPos.X || y == StartPos.Y)
                            {
                                tempColor++;
                            }

                            var mo = house.Add(0x0496, tempColor, x - foundationItem.X, y - foundationItem.Y, (sbyte)z, true);

                            mo.State = CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_TRANSPARENT;
                            mo.AddToTile();
                        }
                    }

                    color += 5;
                    z += 20;
                }
            }
        }

        public void OnTargetWorld(GameObject place)
        {
            if (place != null /*&& place is Multi m*/)
            {
                int zOffset = 0;

                var gump = UIManager.GetGump<HouseCustomizationGump>(Serial);

                if (CurrentFloor == 1)
                {
                    zOffset = -7;
                }

                if (SeekTile)
                {
                    if (place is Multi)
                        SeekGraphic(place.Graphic);
                }
                else if (place.Z >= World.Player.Z + zOffset && place.Z < World.Player.Z + 20)
                {
                    Item foundationItem = World.Items.Get(Serial);

                    if (foundationItem == null || !World.HouseManager.TryGetHouse(Serial, out var house))
                        return;

                    if (Erasing)
                    {
                        if (!(place is Multi))
                            return;

                        if (CanEraseHere(place, out var type))
                        {
                            var multi = house.GetMultiAt(place.X, place.Y);

                            if (multi == null)
                                return;

                            int z = 7 + (CurrentFloor - 1) * 20;

                            if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR || type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF)
                            {
                                z = place.Z - (foundationItem.Z + z) + z;
                            }

                            if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF)
                            {
                                NetClient.Socket.Send(new PCustomHouseDeleteRoof(place.Graphic, place.X - foundationItem.X, place.Y - foundationItem.Y, z));
                            }
                            else
                            {
                                NetClient.Socket.Send(new PCustomHouseDeleteItem(place.Graphic, place.X - foundationItem.X, place.Y - foundationItem.Y, z));
                            }

                            place.Destroy();
                        }
                    }
                    else if (SelectedGraphic != 0)
                    {
                        RawList<CustomBuildObject> list = new RawList<CustomBuildObject>();

                        if (CanBuildHere(list, out var type) && list.Count != 0)
                        {
                            //if (type != CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR && !(place is Multi))
                            //    return;

                            int placeX = place.X;
                            int placeY = place.Y;

                            if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR && CombinedStair)
                            {
                                if (gump.Page >= 0 && gump.Page < Stairs.Count)
                                {
                                    var stair = Stairs[gump.Page];

                                    ushort graphic = 0;

                                    if (SelectedGraphic == stair.North)
                                    {
                                        graphic = (ushort) stair.MultiNorth;
                                    }
                                    else if (SelectedGraphic == stair.East)
                                    {
                                        graphic = (ushort) stair.MultiEast;
                                    }
                                    else if (SelectedGraphic == stair.South)
                                    {
                                        graphic = (ushort) stair.MultiSouth;
                                    }
                                    else if (SelectedGraphic == stair.West)
                                    {
                                        graphic = (ushort) stair.MultiWest;
                                    }

                                    if (graphic != 0)
                                    {
                                        NetClient.Socket.Send(new PCustomHouseAddStair(graphic, placeX - foundationItem.X, placeY - foundationItem.Y));
                                    }
                                }
                            }
                            else
                            {
                                var item = list[0];

                                int x = placeX - foundationItem.X + item.X;
                                int y = placeY - foundationItem.Y + item.Y;

                                var multi = house.Components.Where(s => s.X == placeX + item.X && s.Y == placeY + item.Y);

                                if (multi.Any() || type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                                {
                                    if (!CombinedStair)
                                    {
                                        int minZ = foundationItem.Z + 7 + (CurrentFloor - 1) * 20;
                                        int maxZ = minZ + 20;

                                        if (CurrentFloor == 1)
                                        {
                                            minZ -= 7;
                                        }

                                        foreach (Multi multiObject in multi)
                                        {
                                            int testMinZ = minZ;

                                            if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF) != 0)
                                            {
                                                testMinZ -= 3;
                                            }

                                            if (multiObject.Z < testMinZ ||
                                                multiObject.Z >= maxZ ||
                                                !multiObject.IsCustom ||
                                                (multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL) != 0 /*|| (multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_DONT_REMOVE) != 0*/)
                                                continue;

                                            if ( type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                                            {
                                                if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR) != 0)
                                                {
                                                    multiObject.Destroy();
                                                }
                                            }
                                            else if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF)
                                            {
                                                if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF) != 0)
                                                {
                                                    multiObject.Destroy();
                                                }
                                            }
                                            else if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_FLOOR)
                                            {
                                                if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0)
                                                {
                                                    multiObject.Destroy();
                                                }
                                            }
                                            else
                                            {
                                                if ((multiObject.State & (CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR)) == 0)
                                                {
                                                    multiObject.Destroy();
                                                }
                                            }
                                        }

                                        // todo: remove foundation if no components
                                    }

                                    if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF)
                                    {
                                        NetClient.Socket.Send(new PCustomHouseAddRoof(item.Graphic, x, y, item.Z));
                                    }
                                    else
                                    {
                                        NetClient.Socket.Send(new PCustomHouseAddItem(item.Graphic, x, y));
                                    }
                                }
                            }

                            int xx = placeX - foundationItem.X;
                            int yy = placeY - foundationItem.Y;
                            int z = foundationItem.Z + 7 + (CurrentFloor - 1) * 20;

                            if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR && !CombinedStair)
                            {
                                z = foundationItem.Z;
                            }

                            foreach (CustomBuildObject item in list)
                            {
                                house.Add(item.Graphic, 0, xx + item.X, yy + item.Y, (sbyte) (z + item.Z), true);
                            }
                        }
                    }

                    GenerateFloorPlace();
                    gump.Update();
                }
            }
        }

        private void SeekGraphic(ushort graphic)
        {
            CUSTOM_HOUSE_GUMP_STATE state = 0;
            (int res1, int res2) = ExistsInList(ref state, graphic);

            if (res1 != -1 && res2 != -1)
            {
                State = state;
                var gump = UIManager.GetGump<HouseCustomizationGump>(Serial);

                if (State == CUSTOM_HOUSE_GUMP_STATE.CHGS_WALL || 
                    State == CUSTOM_HOUSE_GUMP_STATE.CHGS_ROOF ||
                    State == CUSTOM_HOUSE_GUMP_STATE.CHGS_MISC)
                {
                    Category = res1;
                    gump.Page = res2;
                }
                else
                {
                    Category = -1;
                    gump.Page = res1;
                }

                gump.UpdateMaxPage();
                SetTargetMulti();
                SelectedGraphic = graphic;
                gump.Update();
            }
        }

        public void SetTargetMulti()
        {
            TargetManager.SetTargetingMulti(0, 0, 0, 0, 0, 0);

            Erasing = false;
            SeekTile = false;
            SelectedGraphic = 0;
            CombinedStair = false;
        }

        public bool CanBuildHere(RawList<CustomBuildObject> list, out CUSTOM_HOUSE_BUILD_TYPE type)
        {
            type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_NORMAL;

            if (SelectedGraphic == 0)
                return false;

            if (CombinedStair)
            {
                if (Components + 10 > MaxComponets)
                {
                    return false;
                }

                (int res1, int res2) = SeekGraphicInCustomHouseObjectList(Stairs, SelectedGraphic);

                if (res1 == -1 || res2 == -1 || res1 >= Stairs.Count)
                {
                    list.Add(new CustomBuildObject(SelectedGraphic));

                    return false;
                }

                var item = Stairs[res1];

                //if (StairMultis.Count == 0)
                {
                    if (SelectedGraphic == item.North)
                    {
                        list.Add(new CustomBuildObject((ushort) item.Block) { X = 0, Y = -3, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = -2, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = -1, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.North) {X = 0, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = -3, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = -2, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.North) {X = 0, Y = -1, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = -3, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.North) {X = 0, Y = -2, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.North) { X = 0, Y = -3, Z = 15});
                    }
                    else if (SelectedGraphic == item.East)
                    {
                       list.Add(new CustomBuildObject((ushort) item.East)  {X = 0, Y = 0, Z = 0});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 1, Y = 0, Z = 0});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 2, Y = 0, Z = 0});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 3, Y = 0, Z = 0});
                       list.Add(new CustomBuildObject((ushort) item.East)  {X = 1, Y = 0, Z = 5});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 2, Y = 0, Z = 5});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 3, Y = 0, Z = 5});
                       list.Add(new CustomBuildObject((ushort) item.East)  {X = 2, Y = 0, Z = 10});
                       list.Add(new CustomBuildObject((ushort) item.Block) {X = 3, Y = 0, Z = 10});
                       list.Add(new CustomBuildObject((ushort) item.East){X = 3, Y = 0, Z = 15});
                    }
                    else if (SelectedGraphic == item.South)
                    {
                        list.Add(new CustomBuildObject((ushort) item.South) {X = 0, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 1, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 2, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 3, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.South) {X = 0, Y = 1, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 2, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 3, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.South) {X = 0, Y = 2, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = 0, Y = 3, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.South) {X = 0, Y = 3, Z = 15});
                    }
                    else if (SelectedGraphic == item.West)
                    {
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -3, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -2, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -1, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.West)  {X = 0, Y = 0, Z = 0});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -3, Y = 0, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -2, Y = 0, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.West)  {X = -1, Y = 0, Z = 5});
                        list.Add(new CustomBuildObject((ushort) item.Block) {X = -3, Y = 0, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.West)  {X = -2, Y = 0, Z = 10});
                        list.Add(new CustomBuildObject((ushort) item.West)  {X = -3, Y = 0, Z = 15});
                    }
                    else
                    {
                        list.Add(new CustomBuildObject(SelectedGraphic));
                    }
                }

                type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR;
            }
            else
            {
                (int fixCheck1, int fixCheck2) = SeekGraphicInCustomHouseObjectList(Doors, SelectedGraphic);

                bool isFixture = false;

                if (fixCheck1 == -1 || fixCheck2 == -1)
                {
                    (fixCheck1, fixCheck2) = SeekGraphicInCustomHouseObjectList(Teleports, SelectedGraphic);

                    isFixture = fixCheck1 != -1 && fixCheck2 != -1;
                }
                else
                {
                    isFixture = true;
                }

                if (isFixture)
                {
                    if (Fixtures + 1 > MaxFixtures)
                    {
                        return false;
                    }
                }
                else if (Components + 1 > MaxComponets)
                {
                    return false;
                }

                if (State == CUSTOM_HOUSE_GUMP_STATE.CHGS_ROOF)
                {
                    list.Add(new CustomBuildObject() { Graphic = SelectedGraphic, Z = (RoofZ - 2) * 3 });
                    type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF;
                }
                else
                {
                    if (State == CUSTOM_HOUSE_GUMP_STATE.CHGS_STAIR)
                    {
                        list.Add(new CustomBuildObject() { Graphic = SelectedGraphic, Y = 1 });
                        type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR;
                    }
                    else
                    {
                        if (State == CUSTOM_HOUSE_GUMP_STATE.CHGS_FLOOR)
                        {
                            type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_FLOOR;
                        }

                        list.Add(new CustomBuildObject() { Graphic = SelectedGraphic });
                    }
                }
            }

            if (SelectedObject.Object is GameObject gobj)
            {
                if (gobj.Z < MinHouseZ)
                {
                    if (CombinedStair)
                    {
                        if (gobj.X >= EndPos.X || gobj.Y > EndPos.Y)
                        {
                            return false;
                        }
                    }
                    else if (type != CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                    {
                        if (gobj.X > EndPos.X - 1 || gobj.Y > EndPos.Y - 1)
                        {
                            return false;
                        }
                    }
                }

                //if ((type != CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR || CombinedStair) && gobj.Z < MinHouseZ &&
                //    (gobj.X == EndPos.X - 1 || gobj.Y == EndPos.Y - 1))
                //{
                //    return false;
                //}

                Item foundationItem = World.Items.Get(Serial);

                int minZ = (foundationItem?.Z ?? 0) + 7 + (CurrentFloor - 1) * 20;
                int maxZ = minZ + 20;

                int boundsOffset = State != CUSTOM_HOUSE_GUMP_STATE.CHGS_WALL ? 1 : 0;

                Rectangle rect = new Rectangle(StartPos.X + boundsOffset, StartPos.Y + boundsOffset, EndPos.X, EndPos.Y);


                foreach (var item in list)
                {
                    if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                    {
                        if (CombinedStair)
                        {
                            if (item.Z != 0)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            int sx = gobj.X + item.X;
                            int sy = gobj.Y + item.Y;

                            if ( !(sx > StartPos.X && sx < EndPos.X && sy >= EndPos.Y && sy <= EndPos.Y + 1)  || gobj.Z >= MinHouseZ)
                            {
                                return false;
                            }

                            if (gobj.Y + item.Y != EndPos.Y)
                            {
                                list[0].Y = 0;
                            }

                            continue;
                        }
                    }

                    if (!ValidateItemPlace(rect, item.Graphic, gobj.X + item.X, gobj.Y + item.Y))
                    {
                        return false;
                    }

                    if (type != CUSTOM_HOUSE_BUILD_TYPE.CHBT_FLOOR && foundationItem != null && World.HouseManager.TryGetHouse(Serial, out var house))
                    {
                        //var multi = house.GetMultiAt(gobj.X + item.X, gobj.Y + item.Y);

                        //if (multi != null)
                        {
                            foreach (Multi multiObject in house.Components.Where(s => s.X == gobj.X + item.X && s.Y == gobj.Y + item.Y))
                            {
                                if (multiObject.IsCustom && (((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL) == 0) &&
                                                             multiObject.Z >= minZ && multiObject.Z < maxZ))
                                {
                                    if (type == CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                                    {
                                        if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) == 0)
                                        {
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if ((multiObject.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR) != 0)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }
        
        public bool CanEraseHere(GameObject place, out CUSTOM_HOUSE_BUILD_TYPE type)
        {
            type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_NORMAL;

            if (place != null && place is Multi multi)
            {
                if (multi.IsCustom && (multi.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_GENERIC_INTERNAL) == 0)
                {
                    if ((multi.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0)
                    {
                        type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_FLOOR;
                    }
                    else if ((multi.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR) != 0)
                    {
                        type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR;
                    }
                    else if ((multi.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF) != 0)
                    {
                        type = CUSTOM_HOUSE_BUILD_TYPE.CHBT_ROOF;
                    }
                    else if (place.X >= StartPos.X && place.X <= EndPos.X &&
                             place.Y >= StartPos.Y && place.Y <= EndPos.Y && place.Z >= MinHouseZ)
                    {

                    }
                    else
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        public (int, int) ExistsInList(ref CUSTOM_HOUSE_GUMP_STATE state, ushort graphic)
        {
            (int res1, int res2) =
                SeekGraphicInCustomHouseObjectListWithCategory<CustomHouseWall,
                    CustomHouseWallCategory>(Walls, graphic);

            if (res1 == -1 || res2 == -1)
            {
                (res1, res2) = SeekGraphicInCustomHouseObjectList(Floors, graphic);

                if (res1 == -1 || res2 == -1)
                {
                    (res1, res2) = SeekGraphicInCustomHouseObjectList(Doors, graphic);

                    if (res1 == -1 || res2 == -1)
                    {
                        (res1, res2) =
                            SeekGraphicInCustomHouseObjectListWithCategory<CustomHouseMisc, CustomHouseMiscCategory>(
                                Miscs, graphic);

                        if (res1 == -1 || res2 == -1)
                        {
                            (res1, res2) = SeekGraphicInCustomHouseObjectList(Stairs, graphic);

                            if (res1 == -1 || res2 == -1)
                            {
                                (res1, res2) =
                                    SeekGraphicInCustomHouseObjectListWithCategory<CustomHouseRoof,
                                        CustomHouseRoofCategory>(Roofs, graphic);

                                if (res1 != -1 && res2 != -1)
                                {
                                    state = CUSTOM_HOUSE_GUMP_STATE.CHGS_ROOF;
                                }
                            }
                            else
                            {
                                state = CUSTOM_HOUSE_GUMP_STATE.CHGS_STAIR;
                            }
                        }
                        else
                        {
                            state = CUSTOM_HOUSE_GUMP_STATE.CHGS_MISC;
                        }
                    }
                    else
                    {
                        state = CUSTOM_HOUSE_GUMP_STATE.CHGS_DOOR;
                    }
                }
                else
                {
                    state = CUSTOM_HOUSE_GUMP_STATE.CHGS_FLOOR;
                }
            }
            else
            {
                state = CUSTOM_HOUSE_GUMP_STATE.CHGS_WALL;
            }

            return (res1, res2);
        }

        private bool ValidateItemPlace(Rectangle rect, ushort graphic, int x, int y)
        {
            if (!rect.Contains(x, y))
                return false;

            (int infoCheck1, int infoCheck2) = SeekGraphicInCustomHouseObjectList(ObjectsInfo, graphic);

            if (infoCheck1 != -1 && infoCheck2 != -1)
            {
                var info = ObjectsInfo[infoCheck1];

                if (info.CanGoW == 0 && x == StartPos.X)
                    return false;

                if (info.CanGoN == 0 && y == StartPos.Y)
                    return false;

                if (info.CanGoNWS == 0 && x == StartPos.X && y == StartPos.Y)
                    return false;
            }

            return true;
        }

        public bool ValidateItemPlace(Item foundationItem, Multi item, int minZ, int maxZ, List<Point> validatedFloors)
        {

            if (item == null || !World.HouseManager.TryGetHouse(foundationItem, out var house) || !item.IsCustom)
                return true;

            if ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0)
            {
                bool existsInList(List<Point> list, Point testedPoint)
                {
                    foreach (Point point in list)
                    {
                        if (testedPoint == point)
                            return true;
                    }

                    return false;
                }

                if (ValidatePlaceStructure(
                        foundationItem,
                        house,
                        house.GetMultiAt(item.X, item.Y),
                        minZ - 20,
                        maxZ - 20,
                        (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_DIRECT_SUPPORT) ||

                    ValidatePlaceStructure(
                        foundationItem,
                        house,
                        house.GetMultiAt(item.X - 1, item.Y - 1),
                        minZ - 20,
                        maxZ - 20,
                        (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_DIRECT_SUPPORT | CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_CANGO_W)) ||

                    ValidatePlaceStructure(
                        foundationItem,
                        house,
                        house.GetMultiAt(item.X, item.Y - 1),
                        minZ - 20,
                        maxZ - 20,
                        (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_DIRECT_SUPPORT | CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_CANGO_N)))
                {
                    Point[] table =
                    {
                        new Point(-1, 0),
                        new Point(0, -1),
                        new Point(1, 0),
                        new Point(0, 1)
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        Point testPoint = new Point(item.X + table[i].X,
                            item.Y + table[i].Y);

                        if (!existsInList(validatedFloors, testPoint))
                        {
                            validatedFloors.Add(testPoint);
                        }
                    }

                    return true;
                }

                return false;
            }

            if ((item.State & (CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR |
                               CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF |
                               CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FIXTURE)) != 0)
            {
                foreach (Multi temp in house.Components)
                {
                    if ((temp.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR) != 0 &&
                        temp.Z >= minZ && temp.Z < maxZ)
                    {
                        if ((temp.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) != 0 &&
                            (temp.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0)
                        {
                            return true;
                        }
                    }
                }

                // TODO ?

                return false;
            }


            (int infoCheck1, int infoCheck2) = SeekGraphicInCustomHouseObjectList(ObjectsInfo, item.Graphic);

            if (infoCheck1 != -1 && infoCheck2 != -1)
            {
                var info = ObjectsInfo[infoCheck1];

                if (info.CanGoW == 0 && item.X == StartPos.X)
                    return false;
                if (info.CanGoN == 0 && item.Y == StartPos.Y)
                    return false;
                if (info.CanGoNWS == 0 && item.X == StartPos.X && item.Y == StartPos.Y)
                    return false;

                if (info.Bottom == 0)
                {
                    bool found = false;

                    if (info.AdjUN != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X, item.Y + 1), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM |
                                   CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_N));
                    }

                    if (!found && info.AdjUE != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X - 1, item.Y), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_E));
                    }

                    if (!found && info.AdjUS != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X, item.Y - 1), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_S));
                    }

                    if (!found && info.AdjUW != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X + 1, item.Y), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_W));
                    }

                    if (!found)
                        return false;
                }

                if (info.Top == 0)
                {
                    bool found = false;

                    if (info.AdjLN != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X, item.Y + 1), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_TOP |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_N));
                    }

                    if (!found && info.AdjLE != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X - 1, item.Y), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_TOP |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_E));
                    }

                    if (!found && info.AdjLS != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X, item.Y - 1), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_TOP |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_S));
                    }

                    if (!found && info.AdjLW != 0)
                    {
                        found = ValidatePlaceStructure(foundationItem, house, house.GetMultiAt(item.X + 1, item.Y), minZ,
                            maxZ,
                            (int)(CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_TOP |
                                  CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_W));
                    }

                    if (!found)
                        return false;
                }
            }

            return true;
        }

        public bool ValidatePlaceStructure(Item foundationItem, House house, Multi multi, int minZ, int maxZ, int flags)
        {
            if (house == null || multi == null)
                return false;


            foreach (Multi item in house.Components)
            {
                List<Point> validatedFloors = new List<Point>();

                if (item.IsCustom && (item.State & (CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FLOOR |
                                                     CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_STAIR |
                                                     CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_ROOF |
                                                     CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_FIXTURE)) == 0 &&
                                       item.Z >= minZ && item.Z < maxZ)
                {
                    (int info1, int info2) = SeekGraphicInCustomHouseObjectList(ObjectsInfo, item.Graphic);

                    if (info1 != -1 && info2 != -1)
                    {
                        var info = ObjectsInfo[info1];

                        if ((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_DIRECT_SUPPORT) != 0)
                        {
                            if ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) != 0 ||
                                info.DirectSupports == 0)
                            {
                                continue;
                            }

                            if ((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_CANGO_W) != 0)
                            {
                                if (info.CanGoW != 0)
                                    return true;
                            }
                            else if ((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_CANGO_N) != 0)
                            {
                                if (info.CanGoN != 0)
                                    return true;
                            }
                            else
                                return true;
                        }
                        else if (
                            (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM) != 0) && info.Bottom != 0) ||
                            (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_TOP) != 0) && info.Top != 0)
                        )
                        {
                            if ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE) == 0)
                            {
                                if (!ValidateItemPlace(foundationItem, item, minZ, maxZ, validatedFloors))
                                {
                                    item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE |
                                                 CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE;
                                }
                                else
                                {
                                    item.State = item.State | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_VALIDATED_PLACE;
                                }
                            }

                            if ((item.State & CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_INCORRECT_PLACE) == 0)
                            {
                                if ((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_BOTTOM) != 0)
                                {
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_N) != 0) && (info.AdjUN != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_E) != 0) && (info.AdjUE != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_S) != 0) && (info.AdjUS != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_W) != 0) && (info.AdjUW != 0))
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_N) != 0) && (info.AdjLN != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_E) != 0) && (info.AdjLE != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_S) != 0) && (info.AdjLS != 0))
                                    {
                                        return true;
                                    }
                                    if (((flags & (int)CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS.CHVCF_W) != 0) && (info.AdjLW != 0))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static void ParseFile<T>(List<T> list, string path) where T : CustomHouseObject, new()
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists)
                return;

            using (StreamReader reader = File.OpenText(file.FullName))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    T item = new T();

                    if (item.Parse(line))
                    {
                        if (item.FeatureMask == 0 || ((int)World.ClientLockedFeatures.Flags & item.FeatureMask) != 0)
                        {
                            list.Add(item);
                        }
                    }

                }
            }
        }

        private static void ParseFileWithCategory<T, U>(List<U> list, string path)
            where T : CustomHouseObject, new()
            where U : CustomHouseObjectCategory<T>, new()
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists)
                return;

            using (StreamReader reader = File.OpenText(file.FullName))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    T item = new T();

                    if (item.Parse(line))
                    {
                        if (item.FeatureMask != 0 && ((int)World.ClientLockedFeatures.Flags & item.FeatureMask) == 0)
                            continue;

                        bool found = false;

                        foreach (var c in list)
                        {
                            if (c.Index == item.Category)
                            {
                                c.Items.Add(item);
                                found = true;
                                break;
                            }
                        }


                        if (!found)
                        {
                            U c = new U
                            {
                                Index = item.Category
                            };
                            c.Items.Add(item);
                            list.Add(c);
                        }
                    }
                }
            }
        }


        private static (int, int) SeekGraphicInCustomHouseObjectListWithCategory<T, U>(List<U> list, ushort graphic)
            where T : CustomHouseObject
            where U : CustomHouseObjectCategory<T>
        {
            for (int i = 0; i < list.Count; i++)
            {
                U c = list[i];

                for (int j = 0; j < c.Items.Count; j++)
                {
                    int contains = c.Items[j].Contains(graphic);

                    if (contains != -1)
                        return (i, j);
                }
            }

            return (-1, -1);
        }

        private static (int, int) SeekGraphicInCustomHouseObjectList<T>(List<T> list, ushort graphic)
            where T : CustomHouseObject
        {
            for (int i = 0; i < list.Count; i++)
            {
                int contains = list[i].Contains(graphic);
                if (contains != -1)
                    return (i, graphic);
            }
            return (-1, -1);
        }
    }

    enum CUSTOM_HOUSE_GUMP_STATE
    {
        CHGS_WALL = 0,
        CHGS_DOOR,
        CHGS_FLOOR,
        CHGS_STAIR,
        CHGS_ROOF,
        CHGS_MISC,
        CHGS_MENU
    }

    enum CUSTOM_HOUSE_FLOOR_VISION_STATE
    {
        CHGVS_NORMAL = 0,
        CHGVS_TRANSPARENT_CONTENT,
        CHGVS_HIDE_CONTENT,
        CHGVS_TRANSPARENT_FLOOR,
        CHGVS_HIDE_FLOOR,
        CHGVS_TRANSLUCENT_FLOOR,
        CHGVS_HIDE_ALL
    }

    enum CUSTOM_HOUSE_BUILD_TYPE
    {
        CHBT_NORMAL = 0,
        CHBT_ROOF,
        CHBT_FLOOR,
        CHBT_STAIR
    }

    [Flags]
    enum CUSTOM_HOUSE_MULTI_OBJECT_FLAGS
    {
        CHMOF_GENERIC_INTERNAL = 0x01,
        CHMOF_FLOOR = 0x02,
        CHMOF_STAIR = 0x04,
        CHMOF_ROOF = 0x08,
        CHMOF_FIXTURE = 0x10,
        CHMOF_TRANSPARENT = 0x20,
        CHMOF_IGNORE_IN_RENDER = 0x40,
        CHMOF_VALIDATED_PLACE = 0x80,
        CHMOF_INCORRECT_PLACE = 0x100,

        CHMOF_DONT_REMOVE = 0x200,
    }

    [Flags]
    enum CUSTOM_HOUSE_VALIDATE_CHECK_FLAGS
    {
        CHVCF_TOP = 0x01,
        CHVCF_BOTTOM = 0x02,
        CHVCF_N = 0x04,
        CHVCF_E = 0x08,
        CHVCF_S = 0x10,
        CHVCF_W = 0x20,
        CHVCF_DIRECT_SUPPORT = 0x40,
        CHVCF_CANGO_W = 0x80,
        CHVCF_CANGO_N = 0x100
    }
}
