using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    public class PlaceWorkerIndoors : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceIndoors".Translate());
            }
            if (loc.GetRoom(map).TouchesMapEdge)
            {
                return new AcceptanceReport("MustPlaceIndoors".Translate());
            }
            return true;
        }
    }
    public class PlaceWorkerUnderRoof : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!map.roofGrid.Roofed(loc))
            {
                return new AcceptanceReport("MustPlaceUnderRoof".Translate());
            }
            return true;
        }
    }    public class PlaceWorkerInSmallRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceInSmallRoom".Translate());
            }
            if (loc.GetRoom(map).CellCount < 16) // At least 4x4
            {
                return new AcceptanceReport("MustPlaceInSmallRoom".Translate());
            }
            return true;
        }
    }
    public class PlaceWorkerInMediumRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceInMediumRoom".Translate());
            }
            if (loc.GetRoom(map).CellCount < 25) // At least 5x5
            {
                return new AcceptanceReport("MustPlaceInMediumRoom".Translate());
            }
            return true;
        }
    }
    public class PlaceWorkerInLargeRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceInLargeRoom".Translate());
            }
            if (loc.GetRoom(map).CellCount < 36) // At least 6x6
            {
                return new AcceptanceReport("MustPlaceInLargeRoom".Translate());
            }
            return true;
        }
    }
    public class PlaceWorkerInBigRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceInBigRoom".Translate());
            }
            if (loc.GetRoom(map).CellCount < 64) // At least 8x8
            {
                return new AcceptanceReport("MustPlaceInBigRoom".Translate());
            }
            return true;
        }
    }

    public class PlaceWorkerOnlyOneSFPerRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceIndoors".Translate());
            }
            
            Room room = loc.GetRoom(map);
            foreach (var thingInRoom in room.ContainedAndAdjacentThings)
            {
                if (thingInRoom.def.placeWorkers == null)
                {
                    continue;
                }
                if ((thingInRoom.def.placeWorkers.Count > 0) &&
                    thingInRoom.def.PlaceWorkers.Any(pw => pw is PlaceWorkerOnlyOneSFPerRoom))
                {
                    return new AcceptanceReport("MustPlaceOnlyOneSFInRoom".Translate());
                }
            }
            
            return true;
        }
    }
    public class PlaceWorkerOnlyOneBSPerRoom : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustPlaceIndoors".Translate());
            }
            
            Room room = loc.GetRoom(map);
            foreach (var thingInRoom in room.ContainedAndAdjacentThings)
            {
                if (thingInRoom.def.placeWorkers == null)
                {
                    continue;
                }
                if ((thingInRoom.def.placeWorkers.Count > 0) &&
                    thingInRoom.def.PlaceWorkers.Any(pw => pw is PlaceWorkerOnlyOneBSPerRoom))
                {
                    return new AcceptanceReport("MustPlaceOnlyOneBSInRoom".Translate());
                }
            }
            
            return true;
        }
    }
    // PlaceWorker that limits building to one per 40 tiles in a whole construction
    public class PlaceWorkerOnlyOnePerFortyTilesInBuilding : PlaceWorker // I can't make this work, hopefully someone more intelligent than me can pick this up in the future (maybe me in the future)
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (loc.GetRoom(map) == null)
            {
                return new AcceptanceReport("MustBeABigEnoughBuilding".Translate());
            }

            Room room = loc.GetRoom(map);
    
            int totalTiles = 0;
            HashSet<Room> checkedRooms = new HashSet<Room>();
            List<Thing> thingsWithPlaceWorker = new List<Thing>();
    
            Queue<Room> roomsToCheck = new Queue<Room>();
            roomsToCheck.Enqueue(room);
    
            while (roomsToCheck.Count > 0)
            {
                Room currentRoom = roomsToCheck.Dequeue();
                if (checkedRooms.Contains(currentRoom))
                {
                    continue;
                }
    
                checkedRooms.Add(currentRoom);
                totalTiles += currentRoom.CellCount;
    
                foreach (IntVec3 Cell in currentRoom.Cells)
                {
                    if (Cell.Impassable(map))
                    {
                        continue;
                    }
                    foreach (IntVec3 adjacentCell in GenAdj.CellsAdjacent8Way(new TargetInfo(Cell, map)))
                    {
                        if (adjacentCell.Impassable(map))
                        {
                            continue;
                        }
                        if (adjacentCell.GetRoom(map) == null)
                        {
                            continue;
                        }
                        Room adjacentRoom = adjacentCell.GetRoom(map);
                        if (adjacentRoom != null && !checkedRooms.Contains(adjacentRoom) && !adjacentRoom.TouchesMapEdge)
                        {
                            roomsToCheck.Enqueue(adjacentRoom);
                        }
                    }
                }
    
                thingsWithPlaceWorker.AddRange(currentRoom.ContainedAndAdjacentThings.Where(t => t.def.placeWorkers.Contains(typeof(PlaceWorkerOnlyOnePerFortyTilesInBuilding))));
            }
    
            int allowedCount = Math.Max(1, totalTiles / 36);
            int actualCount = thingsWithPlaceWorker.Count;
    
            if (actualCount >= allowedCount)
            {
                return new AcceptanceReport("MustBeABigEnoughBuilding".Translate());
            }
    
            return true;
        }
    }
}