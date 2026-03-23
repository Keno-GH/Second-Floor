using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Partial class for bathroom functionality (DBH integration).
    /// Merged from CompBathroomUpgrade.
    /// </summary>
    public partial class CompStaircaseUpgrades
    {
        // =====================================================
        // Bathroom State (merged from CompBathroomUpgrade)
        // =====================================================
        
        // Threshold below which pawns will try to use bathroom (50%)
        private const float BathroomNeedThreshold = 0.5f;
        
        // Cooldown when water is insufficient (1 in-game hour = 2500 ticks)
        private const int BathroomWaterCooldownTicks = 2500;
        
        // Check interval (every ~250 ticks = ~4 seconds)
        private const int BathroomCheckIntervalTicks = 250;
        
        // Duration in ticks for bathroom actions (60 ticks ≈ 1 real second at 1x speed)
        private const int ToiletDurationTicks = 120;    // 2 seconds
        private const int HandWashDurationTicks = 60;   // 1 second
        private const int ShowerDurationTicks = 600;    // 10 seconds
        private const int DrinkDurationTicks = 30;      // 0.5 seconds
        
        // Thought defs for hot/cold showers
        private static ThoughtDef _hotShowerThought;
        private static ThoughtDef _coldShowerThought;
        private static bool _bathroomThoughtsInitialized;
        
        // Cooldown tracker - when we last failed due to insufficient water
        private int lastWaterFailTick = -99999;
        
        // Active bathroom uses - tracks pawns currently using bathroom
        private Dictionary<Pawn, BathroomUseInfo> activeBathroomUses = new Dictionary<Pawn, BathroomUseInfo>();
        
        private enum BathroomUseType
        {
            Toilet,
            Wash,
            Drink
        }
        
        private class BathroomUseInfo : IExposable
        {
            public int completionTick;
            public int startTick;
            public int durationTicks;
            public BathroomUseType useType;
            public Need needToRestore;
            public float startNeedLevel;
            public float targetNeedLevel;
            public bool usedHotWater;
            public float expectedHotWater;
            
            public void ExposeData()
            {
                Scribe_Values.Look(ref completionTick, "completionTick");
                Scribe_Values.Look(ref startTick, "startTick");
                Scribe_Values.Look(ref durationTicks, "durationTicks");
                Scribe_Values.Look(ref useType, "useType");
                Scribe_Values.Look(ref startNeedLevel, "startNeedLevel");
                Scribe_Values.Look(ref targetNeedLevel, "targetNeedLevel");
                Scribe_Values.Look(ref usedHotWater, "usedHotWater");
                Scribe_Values.Look(ref expectedHotWater, "expectedHotWater");
                // Note: needToRestore is not saved - will be re-found after load
            }
        }
        
        // =====================================================
        // Bathroom Methods
        // =====================================================
        
        /// <summary>
        /// Called during PostExposeData to save/load bathroom state.
        /// </summary>
        private void ExposeBathroomData()
        {
            Scribe_Values.Look(ref lastWaterFailTick, "lastWaterFailTick", -99999);
            // Note: activeBathroomUses is not saved - bathroom actions in progress are lost on save/load
            // This is acceptable as they are very short duration
        }
        
        /// <summary>
        /// Called at 250-tick intervals to check sleeping pawns for bathroom needs.
        /// ProcessActiveBathroomUses is called per-tick from CompTick only when needed.
        /// </summary>
        private void TickBathroomInterval()
        {
            // Only run if DBH is active
            if (!DBHReflectionHelper.IsDBHActive)
                return;
            
            // Check cooldown
            if (Find.TickManager.TicksGame - lastWaterFailTick < BathroomWaterCooldownTicks)
                return;
            
            ProcessSleepingPawns();
        }
        
        private void ProcessActiveBathroomUses()
        {
            if (activeBathroomUses.Count == 0)
                return;
            
            var completedPawns = new List<Pawn>();
            int currentTick = Find.TickManager.TicksGame;
            
            foreach (var kvp in activeBathroomUses)
            {
                var pawn = kvp.Key;
                var info = kvp.Value;
                
                // Skip if pawn is no longer valid or no longer sleeping
                if (pawn == null || !pawn.Spawned || pawn.CurJob?.def != JobDefOf.LayDown)
                {
                    completedPawns.Add(pawn);
                    continue;
                }
                
                // Counteract rest gain while using bathroom
                // Get actual rest gain rate from bed's rest effectiveness
                var restNeed = pawn.needs?.rest;
                if (restNeed != null && parent is Building_Bed bed)
                {
                    // Base rest gain is ~0.000142857f per tick (1.0 rest over 7000 ticks)
                    // This is multiplied by bed's rest effectiveness stat
                    float bedRestEffectiveness = bed.GetStatValue(StatDefOf.BedRestEffectiveness);
                    float restGainPerTick = 0.000142857f * bedRestEffectiveness;
                    restNeed.CurLevel -= restGainPerTick;
                }
                
                // Gradually fill the need over the duration
                if (info.needToRestore != null && info.durationTicks > 0)
                {
                    float progress = (float)(currentTick - info.startTick) / info.durationTicks;
                    progress = Mathf.Clamp01(progress);
                    float targetLevel = Mathf.Lerp(info.startNeedLevel, info.targetNeedLevel, progress);
                    info.needToRestore.CurLevel = targetLevel;
                }
                
                // Check if bathroom use is complete
                if (currentTick >= info.completionTick)
                {
                    CompleteBathroomUse(pawn, info);
                    completedPawns.Add(pawn);
                }
            }
            
            // Remove completed uses
            foreach (var pawn in completedPawns)
            {
                activeBathroomUses.Remove(pawn);
            }
        }
        
        private void CompleteBathroomUse(Pawn pawn, BathroomUseInfo info)
        {
            // Ensure need is at target level (in case of any rounding issues)
            if (info.needToRestore != null)
            {
                info.needToRestore.CurLevel = info.targetNeedLevel;
            }
            
            // Apply mood effects for washing
            if (info.useType == BathroomUseType.Wash && pawn.needs?.mood != null)
            {
                InitializeBathroomThoughts();
                
                if (info.usedHotWater && _hotShowerThought != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(_hotShowerThought);
                }
                else if (!info.usedHotWater && info.expectedHotWater > 0 && _coldShowerThought != null)
                {
                    // Only apply cold shower thought if hot water was expected but not available
                    pawn.needs.mood.thoughts.memories.TryGainMemory(_coldShowerThought);
                }
            }
        }
        
        private void ProcessSleepingPawns()
        {
            if (!(parent is Building_Bed bed))
                return;
            
            // Get active bathroom upgrades
            var bathroomUpgrades = new List<StaircaseUpgradeDef>();
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.IsBathroomUpgrade)
                {
                    bathroomUpgrades.Add(upgrade);
                }
            }
            
            if (bathroomUpgrades.Count == 0)
                return;
            
            // Get the bathroom building for water access
            var bathroom = LinkedBathroom as Building_StaircaseBathroom;
            
            // Process each sleeping occupant
            foreach (var pawn in bed.CurOccupants)
            {
                if (pawn == null || !pawn.Spawned)
                    continue;
                
                ProcessPawnNeeds(pawn, bathroomUpgrades, bathroom);
            }
        }
        
        private void ProcessPawnNeeds(Pawn pawn, List<StaircaseUpgradeDef> upgrades, Building_StaircaseBathroom bathroom)
        {
            // Get DBH needs
            var hygieneNeed = DBHReflectionHelper.GetHygieneNeed(pawn);
            var bladderNeed = DBHReflectionHelper.GetBladderNeed(pawn);
            var thirstNeed = DBHReflectionHelper.GetThirstNeed(pawn);
            
            // Find the best upgrade for each need type (highest restore amount / cap)
            StaircaseUpgradeDef bestBladderUpgrade = null;
            StaircaseUpgradeDef bestThirstUpgrade = null;
            StaircaseUpgradeDef bestHygieneUpgrade = null;
            
            foreach (var upgrade in upgrades)
            {
                if (upgrade.bladderRestoreAmount > 0)
                {
                    if (bestBladderUpgrade == null || upgrade.bladderRestoreAmount > bestBladderUpgrade.bladderRestoreAmount)
                        bestBladderUpgrade = upgrade;
                }
                
                if (upgrade.thirstRestoreAmount > 0)
                {
                    if (bestThirstUpgrade == null || upgrade.thirstRestoreAmount > bestThirstUpgrade.thirstRestoreAmount)
                        bestThirstUpgrade = upgrade;
                }
                
                if (upgrade.hygieneRestoreAmount > 0)
                {
                    // For hygiene, prefer higher cap (shower over basin)
                    if (bestHygieneUpgrade == null || upgrade.hygieneMaxCap > bestHygieneUpgrade.hygieneMaxCap)
                        bestHygieneUpgrade = upgrade;
                }
            }
            
            // Skip if pawn is already using bathroom
            if (activeBathroomUses.ContainsKey(pawn))
                return;
            
            // Process bladder (toilet usage)
            if (bladderNeed != null && bladderNeed.CurLevel < BathroomNeedThreshold && bestBladderUpgrade != null)
            {
                if (TryStartBathroomUse(pawn, bestBladderUpgrade, bathroom, BathroomUseType.Toilet, bladderNeed, bestBladderUpgrade.bladderRestoreAmount))
                    return; // Only one bathroom action at a time
            }
            
            // Process thirst (drinking water)
            if (thirstNeed != null && thirstNeed.CurLevel < BathroomNeedThreshold && bestThirstUpgrade != null)
            {
                if (TryStartBathroomUse(pawn, bestThirstUpgrade, bathroom, BathroomUseType.Drink, thirstNeed, bestThirstUpgrade.thirstRestoreAmount))
                    return;
            }
            
            // Process hygiene (washing/showering)
            if (hygieneNeed != null && hygieneNeed.CurLevel < BathroomNeedThreshold && bestHygieneUpgrade != null)
            {
                // Don't wash if already above the best cap
                if (hygieneNeed.CurLevel < bestHygieneUpgrade.hygieneMaxCap)
                {
                    float targetLevel = Mathf.Min(bestHygieneUpgrade.hygieneRestoreAmount, bestHygieneUpgrade.hygieneMaxCap);
                    TryStartBathroomUse(pawn, bestHygieneUpgrade, bathroom, BathroomUseType.Wash, hygieneNeed, targetLevel);
                }
            }
        }
        
        private int GetDurationForUseType(BathroomUseType useType, StaircaseUpgradeDef upgrade)
        {
            switch (useType)
            {
                case BathroomUseType.Toilet:
                    return ToiletDurationTicks;
                case BathroomUseType.Wash:
                    // Basin (75% cap) uses hand wash duration, shower (100% cap) uses shower duration
                    return upgrade.hygieneMaxCap >= 1.0f ? ShowerDurationTicks : HandWashDurationTicks;
                case BathroomUseType.Drink:
                    return DrinkDurationTicks;
                default:
                    return 60;
            }
        }
        
        private bool TryStartBathroomUse(Pawn pawn, StaircaseUpgradeDef upgrade, Building_StaircaseBathroom bathroom, 
            BathroomUseType useType, Need needToRestore, float restoreAmount)
        {
            float waterNeeded = 0f;
            float hotWaterNeeded = 0f;
            float sewageProduced = 0f;
            bool usedHotWater = false;
            
            switch (useType)
            {
                case BathroomUseType.Toilet:
                    waterNeeded = DBHReflectionHelper.ToiletWaterUse;
                    sewageProduced = DBHReflectionHelper.ToiletSewageOutput;
                    break;
                case BathroomUseType.Wash:
                    waterNeeded = upgrade.waterPerUse > 0 ? upgrade.waterPerUse : DBHReflectionHelper.ShowerWaterUse;
                    hotWaterNeeded = upgrade.hotWaterPerUse;
                    sewageProduced = upgrade.sewagePerUse;
                    break;
                case BathroomUseType.Drink:
                    waterNeeded = DBHReflectionHelper.DrinkWaterUse;
                    break;
            }
            
            // Check if we have enough water
            if (bathroom != null && waterNeeded > 0)
            {
                if (!bathroom.HasEnoughWater(waterNeeded))
                {
                    lastWaterFailTick = Find.TickManager.TicksGame;
                    return false;
                }
                
                // Try to use hot water first for washing
                if (useType == BathroomUseType.Wash && hotWaterNeeded > 0)
                {
                    if (bathroom.HasEnoughHotWater(hotWaterNeeded))
                    {
                        if (!bathroom.TryUseHotWater(hotWaterNeeded))
                        {
                            lastWaterFailTick = Find.TickManager.TicksGame;
                            return false;
                        }
                        usedHotWater = true;
                        // Reduce cold water needed since we used hot water
                        waterNeeded = Mathf.Max(0, waterNeeded - hotWaterNeeded);
                    }
                }
                
                // Use cold water
                if (waterNeeded > 0)
                {
                    if (!bathroom.TryUseWater(waterNeeded))
                    {
                        lastWaterFailTick = Find.TickManager.TicksGame;
                        return false;
                    }
                }
                
                // Push sewage
                if (sewageProduced > 0)
                {
                    bathroom.PushSewage(sewageProduced);
                }
            }
            else if (waterNeeded > 0)
            {
                // No bathroom building but water is needed - can't use
                return false;
            }
            
            // Queue the bathroom use with duration - need is gradually restored over duration
            int duration = GetDurationForUseType(useType, upgrade);
            int currentTick = Find.TickManager.TicksGame;
            activeBathroomUses[pawn] = new BathroomUseInfo
            {
                completionTick = currentTick + duration,
                startTick = currentTick,
                durationTicks = duration,
                useType = useType,
                needToRestore = needToRestore,
                startNeedLevel = needToRestore?.CurLevel ?? 0f,
                targetNeedLevel = restoreAmount,
                usedHotWater = usedHotWater,
                expectedHotWater = hotWaterNeeded
            };
            
            return true;
        }
        
        private static void InitializeBathroomThoughts()
        {
            if (_bathroomThoughtsInitialized)
                return;
            
            _bathroomThoughtsInitialized = true;
            
            // Try to get DBH's shower thoughts
            _hotShowerThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("HotShower");
            _coldShowerThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ColdShower");
        }
        
        /// <summary>
        /// Gets the bathroom inspect string extra.
        /// </summary>
        private string GetBathroomInspectString()
        {
            if (!DBHReflectionHelper.IsDBHActive)
                return null;
            
            // Check if we have any bathroom upgrades
            bool hasBathroom = false;
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.IsBathroomUpgrade)
                {
                    hasBathroom = true;
                    break;
                }
            }
            
            if (!hasBathroom)
                return null;
            
            var bathroom = LinkedBathroom as Building_StaircaseBathroom;
            if (bathroom == null)
                return "SF_BathroomNoPipes".Translate();
            
            if (!bathroom.HasWaterConnection)
                return "SF_BathroomNoWater".Translate();
            
            return null;
        }
    }
}
