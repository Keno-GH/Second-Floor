using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    class CompatibilityPatches
    {
        public static void ExecuteCompatibilityPatches(Harmony harmony)
        {
            HospitalityPatches.ApplyPatches(harmony);
        }
    }
    
    /// <summary>
    /// Hospitality mod compatibility patches.
    /// Patches BedUtility.StaticBedValue to use virtual room stats from Second Floor upgrades
    /// instead of physical room stats.
    /// Also patches Building_GuestBed.Swap to preserve staircase upgrades during conversion.
    /// </summary>
    public static class HospitalityPatches
    {
        private static Type bedUtilityType;
        private static Type buildingGuestBedType;
        private static MethodInfo updateStatsMethod;
        
        /// <summary>
        /// Temporary storage for upgrade data during bed swap operations.
        /// Key: bed position + map ID, Value: upgrade data to transfer
        /// </summary>
        private static Dictionary<(IntVec3 pos, int mapId), SwapUpgradeData> pendingSwapData = new Dictionary<(IntVec3, int), SwapUpgradeData>();
        
        private class SwapUpgradeData
        {
            public List<ActiveUpgrade> constructedUpgrades;
            public float targetTemperature;
            public bool preferFueledFirst;
        }
        
        public static bool IsHospitalityLoaded => buildingGuestBedType != null;
        
        public static void ApplyPatches(Harmony harmony)
        {
            // Try to find Hospitality types
            buildingGuestBedType = AccessTools.TypeByName("Hospitality.Building_GuestBed");
            if (buildingGuestBedType == null)
            {
                return;
            }
            
            bedUtilityType = AccessTools.TypeByName("Hospitality.Utilities.BedUtility");
            if (bedUtilityType == null)
            {
                Log.Warning("[Second Floor] Hospitality detected but BedUtility type not found.");
                return;
            }
            
            // Cache UpdateStats method for triggering recalculation
            updateStatsMethod = AccessTools.Method(buildingGuestBedType, "UpdateStats");
            
            // Patch StaticBedValue method
            var staticBedValueMethod = AccessTools.Method(bedUtilityType, "StaticBedValue");
            if (staticBedValueMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(HospitalityPatches), nameof(StaticBedValue_Postfix));
                harmony.Patch(staticBedValueMethod, postfix: postfix);
            }
            else
            {
                Log.Warning("[Second Floor] Could not find BedUtility.StaticBedValue method to patch.");
            }
            
            // Patch OtherOwnerScore to skip opinion check for Second Floor beds (pawns don't share rooms)
            var otherOwnerScoreMethod = AccessTools.Method(bedUtilityType, "OtherOwnerScore");
            if (otherOwnerScoreMethod != null)
            {
                var prefix = new HarmonyMethod(typeof(HospitalityPatches), nameof(OtherOwnerScore_Prefix));
                harmony.Patch(otherOwnerScoreMethod, prefix: prefix);
            }
            
            // Patch Building_GuestBed.Swap to preserve staircase upgrades during conversion
            var swapMethod = AccessTools.Method(buildingGuestBedType, "Swap", new Type[] { typeof(Building_Bed) });
            if (swapMethod != null)
            {
                var prefix = new HarmonyMethod(typeof(HospitalityPatches), nameof(Swap_Prefix));
                var postfix = new HarmonyMethod(typeof(HospitalityPatches), nameof(Swap_Postfix));
                harmony.Patch(swapMethod, prefix: prefix, postfix: postfix);
            }
            else
            {
                Log.Warning("[Second Floor] Could not find Building_GuestBed.Swap method to patch.");
            }
        }
        
        /// <summary>
        /// Prefix patch for Building_GuestBed.Swap.
        /// Captures upgrade data from the bed being swapped before it's destroyed.
        /// </summary>
        public static void Swap_Prefix(Building_Bed bed)
        {
            if (bed == null || bed.Map == null)
            {
                return;
            }
            
            var upgradesComp = bed.GetComp<CompStaircaseUpgrades>();
            if (upgradesComp == null)
            {
                return;
            }
            
            // Only save if there are actual upgrades to preserve
            if (upgradesComp.constructedUpgrades == null || upgradesComp.constructedUpgrades.Count == 0)
            {
                return;
            }
            
            // Store upgrade data keyed by position and map
            var key = (bed.Position, bed.Map.uniqueID);
            pendingSwapData[key] = new SwapUpgradeData
            {
                constructedUpgrades = upgradesComp.constructedUpgrades.Select(au => new ActiveUpgrade
                {
                    def = au.def,
                    stuff = au.stuff,
                    count = au.count,
                    isToggledOff = au.isToggledOff
                }).ToList(),
                targetTemperature = upgradesComp.targetTemperature,
                preferFueledFirst = upgradesComp.preferFueledFirst
            };
        }
        
        /// <summary>
        /// Postfix patch for Building_GuestBed.Swap.
        /// Applies saved upgrade data to the newly spawned bed.
        /// </summary>
        public static void Swap_Postfix(Building_Bed bed)
        {
            if (bed == null)
            {
                return;
            }
            
            // The original bed parameter is the OLD bed that was despawned.
            // We need to find the new bed at the same position.
            // After Swap, the old bed is destroyed and a new one is spawned at the same position.
            // The 'bed' parameter still references the old (now despawned) bed.
            
            // Find the new bed by checking what's selected (Swap calls Find.Selector.Select on the new bed)
            Building_Bed newBed = null;
            foreach (var obj in Find.Selector.SelectedObjects)
            {
                if (obj is Building_Bed selectedBed && selectedBed.Spawned)
                {
                    newBed = selectedBed;
                    break;
                }
            }
            
            if (newBed == null)
            {
                return;
            }
            
            // Look up pending swap data by the new bed's position
            var key = (newBed.Position, newBed.Map.uniqueID);
            if (!pendingSwapData.TryGetValue(key, out var swapData))
            {
                return;
            }
            
            // Remove from pending data
            pendingSwapData.Remove(key);
            
            // Apply upgrades to the new bed
            var newUpgradesComp = newBed.GetComp<CompStaircaseUpgrades>();
            if (newUpgradesComp == null)
            {
                return;
            }
            
            // Transfer upgrade data
            newUpgradesComp.constructedUpgrades = swapData.constructedUpgrades;
            newUpgradesComp.targetTemperature = swapData.targetTemperature;
            newUpgradesComp.preferFueledFirst = swapData.preferFueledFirst;
            
            // Invalidate caches so the new bed recalculates its stats
            newUpgradesComp.InvalidateBedCountCache();
        }
        
        /// <summary>
        /// Postfix patch for BedUtility.StaticBedValue.
        /// Replaces the calculated values with virtual room stats when the bed is a Second Floor staircase.
        /// </summary>
        public static void StaticBedValue_Postfix(
            object bed,
            ref Room room,
            ref int quality,
            ref int impressiveness,
            ref int roomTypeScore,
            ref int comfort,
            ref int facilities,
            ref int __result)
        {
            if (bed == null) return;
            
            // Cast to Building_Bed to access GetComp
            var buildingBed = bed as Building_Bed;
            if (buildingBed == null) return;
            
            var upgradesComp = buildingBed.GetComp<CompStaircaseUpgrades>();
            if (upgradesComp == null) return;
            
            // This is a Second Floor staircase - use virtual stats instead of physical room
            var virtualStats = upgradesComp.CalculateVirtualBedStats();
            
            // Override all output parameters with virtual values
            room = null; // No physical room for virtual floors
            quality = virtualStats.quality;
            impressiveness = virtualStats.impressiveness;
            roomTypeScore = virtualStats.roomTypeScore;
            comfort = virtualStats.comfort;
            facilities = virtualStats.facilities;
            
            // Recalculate the return value
            __result = virtualStats.TotalValue;
        }
        
        /// <summary>
        /// Prefix patch for BedUtility.OtherOwnerScore.
        /// For Second Floor staircases, each pawn has their own private virtual room,
        /// so we skip the opinion check entirely (they're not actually sharing a room).
        /// </summary>
        public static bool OtherOwnerScore_Prefix(Building_Bed bed, Pawn guest, ref int __result)
        {
            if (bed == null) return true;
            
            var upgradesComp = bed.GetComp<CompStaircaseUpgrades>();
            if (upgradesComp == null) return true;
            
            // This is a Second Floor staircase - pawns don't share rooms, so no opinion penalty
            __result = 0;
            return false; // Skip original method
        }
        
        /// <summary>
        /// Triggers Hospitality to recalculate a guest bed's stats.
        /// Called when staircase upgrades change.
        /// </summary>
        public static void TriggerGuestBedStatsUpdate(Building_Bed bed)
        {
            if (!IsHospitalityLoaded || bed == null) return;
            if (updateStatsMethod == null) return;
            
            // Check if this bed is a guest bed (Building_GuestBed type)
            if (!buildingGuestBedType.IsAssignableFrom(bed.GetType())) return;
            
            try
            {
                updateStatsMethod.Invoke(bed, null);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Second Floor] Failed to trigger Hospitality stats update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if a staircase def is in the upgrade's applyToStairs list,
        /// accounting for Hospitality guest bed suffix.
        /// When Hospitality is loaded and a bed is converted to a guest bed,
        /// its defName gets "Guest" appended (e.g., SF_Upstairs -> SF_UpstairsGuest).
        /// This method handles that case by also checking the base defName.
        /// </summary>
        /// <param name="staircaseDef">The ThingDef of the staircase to check</param>
        /// <param name="applyToStairs">The list of allowed staircase defs from the upgrade</param>
        /// <returns>True if the staircase is allowed for this upgrade</returns>
        public static bool IsStaircaseAllowedForUpgrade(ThingDef staircaseDef, List<ThingDef> applyToStairs)
        {
            if (applyToStairs == null)
            {
                return true; // No restriction
            }
            
            // Direct match
            if (applyToStairs.Contains(staircaseDef))
            {
                return true;
            }
            
            // If Hospitality is loaded, check if this is a guest bed version of an allowed staircase
            if (!IsHospitalityLoaded)
            {
                return false;
            }
            
            // Check if defName ends with "Guest" and the base version is allowed
            string defName = staircaseDef.defName;
            if (!defName.EndsWith("Guest"))
            {
                return false;
            }
            
            // Get the base defName (remove "Guest" suffix)
            string baseDefName = defName.Substring(0, defName.Length - 5);
            ThingDef baseDef = DefDatabase<ThingDef>.GetNamedSilentFail(baseDefName);
            
            if (baseDef != null && applyToStairs.Contains(baseDef))
            {
                return true;
            }
            
            return false;
        }
    }
    
    class ShowHairPatches
    {
        public static bool Patch_PawnRenderer_RenderPawnInternal_Postfix_Prefix(Pawn ___pawn)
        {
            if (___pawn?.CurrentBed()?.def?.HasModExtension<SecondFloorModExtension>() == true)
            {
                return false;
            }
            return true;
        }
    }

    class FacialHairStuffPatches
    {
        public static bool HarmonyPatch_PawnRenderer_Prefix_Prefix(PawnRenderer __instance)
        {
            Pawn pawn = __instance.renderTree.pawn;
            if (pawn != null)
            {
                Building_Bed bed = pawn.CurrentBed();
                if (bed != null)
                {
                    ThingDef bedDef = bed.def;
                    if (bedDef != null)
                    {
                        bool hasModExtension = bedDef.HasModExtension<SecondFloorModExtension>();
                        if (hasModExtension)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
