using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Prevents placement on gravship substructure tiles.
    /// Used for staircases that cannot be built on gravships.
    /// </summary>
    public class PlaceWorker_NotOnGravship : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!ModsConfig.IsActive("ludeon.rimworld.odyssey"))
            {
                return true;
            }

            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                TerrainDef foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.IsSubstructure)
                {
                    return new AcceptanceReport("SF_CannotPlaceOnGravship".Translate());
                }
            }

            return true;
        }
    }

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
    }
    
    /// <summary>
    /// Requires placement under overhead mountain (thick rock) or thin rock roof.
    /// Used for mountain upfloor staircases that build into the mountain overhead.
    /// </summary>
    public class PlaceWorker_UnderMountainRoof : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (DebugSettings.godMode)
            {
                return true;
            }
            
            RoofDef roof = loc.GetRoof(map);
            if (roof != RoofDefOf.RoofRockThick && roof != RoofDefOf.RoofRockThin)
            {
                return new AcceptanceReport("SF_MustPlaceUnderMountain".Translate());
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
    
    /// <summary>
    /// Visualizes the staircase's area of influence during placement.
    /// Shows a 20x20 circular area and highlights cells with constructed roofs.
    /// </summary>
    public class PlaceWorker_StaircaseVisualizer : PlaceWorker
    {
        private static readonly Color AreaRingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color RoofHighlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        
        // Reusable list to avoid allocations during DrawGhost
        private static List<IntVec3> roofedCellsCache = new List<IntVec3>();
        
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Draw the area ring (radius 10 for 20x20 area)
            GenDraw.DrawRadiusRing(center, 10f, AreaRingColor);
            
            // Collect cells with constructed roofs
            roofedCellsCache.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofConstructed)
                {
                    roofedCellsCache.Add(cell);
                }
            }
            
            // Highlight roofed cells in green
            if (roofedCellsCache.Count > 0)
            {
                GenDraw.DrawFieldEdges(roofedCellsCache, RoofHighlightColor);
            }
        }
        
        public override void DrawPlaceMouseAttachments(float curX, ref float curY, BuildableDef bdef, IntVec3 center, Rot4 rot)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Count constructed roof cells
            int roofedCount = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofConstructed)
                {
                    roofedCount++;
                }
            }
            
            // Display the available space count
            string label = "SF_AvailableSpace".Translate(roofedCount);
            Color textColor = roofedCount > 0 ? Color.green : Color.red;
            
            Widgets.Label(new Rect(curX, curY, 999f, 999f), label.Colorize(textColor));
            curY += 22f;
        }
    }
    
    /// <summary>
    /// Visualizes the mountain staircase's area of influence during placement.
    /// Shows a 20x20 circular area and highlights cells with overhead mountain or thin rock roofs.
    /// </summary>
    public class PlaceWorker_MountainStaircaseVisualizer : PlaceWorker
    {
        private static readonly Color AreaRingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color MountainRoofHighlightColor = new Color(0.6f, 0.4f, 0.2f, 0.5f);
        
        // Reusable list to avoid allocations during DrawGhost
        private static List<IntVec3> mountainRoofCellsCache = new List<IntVec3>();
        
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Draw the area ring (radius 10 for 20x20 area)
            GenDraw.DrawRadiusRing(center, 10f, AreaRingColor);
            
            // Collect cells with mountain roofs (thick or thin rock)
            mountainRoofCellsCache.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin)
                {
                    mountainRoofCellsCache.Add(cell);
                }
            }
            
            // Highlight mountain roof cells in brown/earthy color
            if (mountainRoofCellsCache.Count > 0)
            {
                GenDraw.DrawFieldEdges(mountainRoofCellsCache, MountainRoofHighlightColor);
            }
        }
        
        public override void DrawPlaceMouseAttachments(float curX, ref float curY, BuildableDef bdef, IntVec3 center, Rot4 rot)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Count mountain roof cells (thick or thin rock)
            int mountainRoofCount = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin)
                {
                    mountainRoofCount++;
                }
            }
            
            // Display the available space count
            string label = "SF_AvailableMountainSpace".Translate(mountainRoofCount);
            Color textColor = mountainRoofCount > 0 ? Color.green : Color.red;
            
            Widgets.Label(new Rect(curX, curY, 999f, 999f), label.Colorize(textColor));
            curY += 22f;
        }
    }
    
    /// <summary>
    /// Requires staircases of the same floor level to be at least 10 cells apart.
    /// </summary>
    public class PlaceWorker_MinStaircaseDistance : PlaceWorker
    {
        private const float MinDistance = 10f;
        private static readonly Color ExclusionZoneColor = new Color(1f, 0.2f, 0.2f, 0.4f);
        
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (DebugSettings.godMode)
            {
                return true;
            }
            
            // Get the floor level of what we're placing
            ThingDef thingDef = checkingDef as ThingDef;
            if (thingDef == null)
            {
                return true;
            }
            
            SecondFloorModExtension ext = thingDef.GetModExtension<SecondFloorModExtension>();
            if (ext == null)
            {
                return true;
            }
            
            StaircaseFloorLevel placingFloorLevel = ext.floorLevel;
            
            // Check all buildings on the map for same-type staircases
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (building == thingToIgnore)
                {
                    continue;
                }
                
                SecondFloorModExtension buildingExt = building.def.GetModExtension<SecondFloorModExtension>();
                if (buildingExt == null)
                {
                    continue;
                }
                
                // Only check staircases of the same floor level
                if (buildingExt.floorLevel != placingFloorLevel)
                {
                    continue;
                }
                
                // Check if this building has CompStaircaseUpgrades (meaning it's a staircase)
                if (building.GetComp<CompStaircaseUpgrades>() == null)
                {
                    continue;
                }
                
                // Check distance
                if (loc.InHorDistOf(building.Position, MinDistance))
                {
                    return new AcceptanceReport("SF_TooCloseToAnotherStaircase".Translate());
                }
            }
            
            // Also check blueprints and frames for same-type staircases
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (t == thingToIgnore)
                {
                    continue;
                }
                
                ThingDef builtDef = t.def.entityDefToBuild as ThingDef;
                if (builtDef == null)
                {
                    continue;
                }
                
                SecondFloorModExtension blueprintExt = builtDef.GetModExtension<SecondFloorModExtension>();
                if (blueprintExt == null || blueprintExt.floorLevel != placingFloorLevel)
                {
                    continue;
                }
                
                if (builtDef.GetCompProperties<CompProperties_StaircaseUpgrades>() == null)
                {
                    continue;
                }
                
                if (loc.InHorDistOf(t.Position, MinDistance))
                {
                    return new AcceptanceReport("SF_TooCloseToAnotherStaircase".Translate());
                }
            }
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (t == thingToIgnore)
                {
                    continue;
                }
                
                ThingDef builtDef = t.def.entityDefToBuild as ThingDef;
                if (builtDef == null)
                {
                    continue;
                }
                
                SecondFloorModExtension frameExt = builtDef.GetModExtension<SecondFloorModExtension>();
                if (frameExt == null || frameExt.floorLevel != placingFloorLevel)
                {
                    continue;
                }
                
                if (builtDef.GetCompProperties<CompProperties_StaircaseUpgrades>() == null)
                {
                    continue;
                }
                
                if (loc.InHorDistOf(t.Position, MinDistance))
                {
                    return new AcceptanceReport("SF_TooCloseToAnotherStaircase".Translate());
                }
            }
            
            return true;
        }
        
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            SecondFloorModExtension ext = def.GetModExtension<SecondFloorModExtension>();
            if (ext == null)
            {
                return;
            }
            
            StaircaseFloorLevel placingFloorLevel = ext.floorLevel;
            
            // Draw exclusion rings around existing same-type staircases
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                SecondFloorModExtension buildingExt = building.def.GetModExtension<SecondFloorModExtension>();
                if (buildingExt == null || buildingExt.floorLevel != placingFloorLevel)
                {
                    continue;
                }
                
                if (building.GetComp<CompStaircaseUpgrades>() == null)
                {
                    continue;
                }
                
                GenDraw.DrawRadiusRing(building.Position, MinDistance, ExclusionZoneColor);
            }
        }
    }
    
    /// <summary>
    /// Requires placement on gravship substructure tiles.
    /// Used for gravship staircases that can only be built on gravships.
    /// </summary>
    public class PlaceWorker_OnGravship : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!ModsConfig.IsActive("ludeon.rimworld.odyssey"))
            {
                return new AcceptanceReport("SF_RequiresOdyssey".Translate());
            }

            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                TerrainDef foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation == null || !foundation.IsSubstructure)
                {
                    return new AcceptanceReport("SF_MustPlaceOnGravship".Translate());
                }
            }

            return true;
        }
    }
    
    /// <summary>
    /// Visualizes the gravship upstairs staircase's area of influence during placement.
    /// Shows a 20x20 circular area and highlights cells with gravship substructures.
    /// </summary>
    public class PlaceWorker_GravshipStaircaseVisualizer : PlaceWorker
    {
        private static readonly Color AreaRingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color SubstructureHighlightColor = new Color(0.2f, 0.6f, 0.9f, 0.5f);
        
        // Reusable list to avoid allocations during DrawGhost
        private static List<IntVec3> substructureCellsCache = new List<IntVec3>();
        
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Draw the area ring (radius 10 for 20x20 area)
            GenDraw.DrawRadiusRing(center, 10f, AreaRingColor);
            
            // Collect cells with gravship substructures
            substructureCellsCache.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                TerrainDef foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.IsSubstructure)
                {
                    substructureCellsCache.Add(cell);
                }
            }
            
            // Highlight substructure cells in blue
            if (substructureCellsCache.Count > 0)
            {
                GenDraw.DrawFieldEdges(substructureCellsCache, SubstructureHighlightColor);
            }
        }
        
        public override void DrawPlaceMouseAttachments(float curX, ref float curY, BuildableDef bdef, IntVec3 center, Rot4 rot)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            
            // Count gravship substructure cells
            int substructureCount = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                TerrainDef foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.IsSubstructure)
                {
                    substructureCount++;
                }
            }
            
            // Display the available space count
            string label = "SF_AvailableGravshipSpace".Translate(substructureCount);
            Color textColor = substructureCount > 0 ? Color.green : Color.red;
            
            Widgets.Label(new Rect(curX, curY, 999f, 999f), label.Colorize(textColor));
            curY += 22f;
        }
    }
}