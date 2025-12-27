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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
            if (DebugSettings.godMode)
            {
                return true;
            }
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
}