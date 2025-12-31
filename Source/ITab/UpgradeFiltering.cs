using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// The type of tab currently selected in the staircase ITab.
    /// </summary>
    public enum UpgradeTabType
    {
        Manage,      // Currently built upgrades - toggle and deconstruct
        Control,     // Toggleable upgrades only - quick on/off switches
        Construction // All available upgrades - build new ones
    }
    
    /// <summary>
    /// Static helper methods for filtering and organizing staircase upgrades.
    /// </summary>
    public static class UpgradeFiltering
    {
        /// <summary>
        /// Gets all upgrades applicable to the given staircase (filtered by stair type and research).
        /// </summary>
        public static List<StaircaseUpgradeDef> GetApplicableUpgrades(Thing staircase, CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> result = new List<StaircaseUpgradeDef>();
            
            foreach (var def in DefDatabase<StaircaseUpgradeDef>.AllDefs)
            {
                // Skip if not applicable to this staircase type
                if (def.applyToStairs != null && !def.applyToStairs.Contains(staircase.def))
                {
                    continue;
                }
                
                // Skip if research prerequisite is not completed (unless God mode is on)
                if (!DebugSettings.godMode && def.researchPrerequisite != null && !def.researchPrerequisite.IsFinished)
                {
                    continue;
                }
                
                result.Add(def);
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets only the upgrades that are currently constructed (for Manage tab).
        /// </summary>
        public static List<StaircaseUpgradeDef> GetBuiltUpgrades(CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> result = new List<StaircaseUpgradeDef>();
            
            foreach (var activeUpgrade in comp.constructedUpgrades)
            {
                if (!result.Contains(activeUpgrade.def))
                {
                    result.Add(activeUpgrade.def);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets only the toggleable upgrades that are currently constructed (for Control tab).
        /// </summary>
        public static List<StaircaseUpgradeDef> GetToggleableUpgrades(CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> result = new List<StaircaseUpgradeDef>();
            
            foreach (var activeUpgrade in comp.constructedUpgrades)
            {
                if (activeUpgrade.def.isToggleable && !result.Contains(activeUpgrade.def))
                {
                    result.Add(activeUpgrade.def);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Groups upgrades by their category, sorted by category displayOrder then upgrade displayPriority.
        /// Categories with no applicable upgrades are excluded.
        /// </summary>
        public static Dictionary<UpgradeCategoryDef, List<StaircaseUpgradeDef>> GroupByCategory(List<StaircaseUpgradeDef> upgrades)
        {
            var grouped = new Dictionary<UpgradeCategoryDef, List<StaircaseUpgradeDef>>();
            
            // Group upgrades by category
            foreach (var upgrade in upgrades)
            {
                if (upgrade.category == null)
                {
                    continue; // Skip uncategorized upgrades
                }
                
                if (!grouped.ContainsKey(upgrade.category))
                {
                    grouped[upgrade.category] = new List<StaircaseUpgradeDef>();
                }
                grouped[upgrade.category].Add(upgrade);
            }
            
            // Sort upgrades within each category by displayPriority
            foreach (var category in grouped.Keys.ToList())
            {
                grouped[category] = grouped[category]
                    .OrderBy(u => u.displayPriority)
                    .ThenBy(u => u.label)
                    .ToList();
            }
            
            // Return sorted by category displayOrder
            return grouped
                .OrderBy(kvp => kvp.Key.displayOrder)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        /// <summary>
        /// Gets the list of pending upgrades (blueprints or frames) for a staircase.
        /// </summary>
        public static List<StaircaseUpgradeDef> GetPendingUpgrades(Thing staircase)
        {
            List<StaircaseUpgradeDef> pending = new List<StaircaseUpgradeDef>();
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Blueprint blueprint = t as Blueprint;
                if (blueprint == null)
                {
                    continue;
                }
                
                ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef != null && !pending.Contains(ext.upgradeDef))
                {
                    pending.Add(ext.upgradeDef);
                }
            }

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Frame frame = t as Frame;
                if (frame == null)
                {
                    continue;
                }
                
                ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef != null && !pending.Contains(ext.upgradeDef))
                {
                    pending.Add(ext.upgradeDef);
                }
            }

            return pending;
        }
        
        /// <summary>
        /// Gets the count of pending blueprints/frames for a specific upgrade.
        /// </summary>
        public static int GetPendingUpgradeCount(Thing staircase, StaircaseUpgradeDef def)
        {
            int count = 0;
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Blueprint blueprint = t as Blueprint;
                if (blueprint == null)
                {
                    continue;
                }
                
                ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef == def)
                {
                    count++;
                }
            }

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Frame frame = t as Frame;
                if (frame == null)
                {
                    continue;
                }
                
                ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef == def)
                {
                    count++;
                }
            }

            return count;
        }
        
        /// <summary>
        /// Checks if an upgrade is locked (prerequisites not met).
        /// </summary>
        public static bool IsUpgradeLocked(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
        {
            if (def.requiredUpgrades == null || def.requiredUpgrades.Count == 0)
            {
                return false;
            }

            foreach (var requiredUpgrade in def.requiredUpgrades)
            {
                if (!comp.HasActiveUpgrade(requiredUpgrade))
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Gets all upgrades that this upgrade would unlock.
        /// </summary>
        public static List<StaircaseUpgradeDef> GetUpgradesUnlockedBy(StaircaseUpgradeDef def)
        {
            List<StaircaseUpgradeDef> unlockedUpgrades = new List<StaircaseUpgradeDef>();
            
            foreach (var otherDef in DefDatabase<StaircaseUpgradeDef>.AllDefsListForReading)
            {
                if (otherDef.requiredUpgrades != null && otherDef.requiredUpgrades.Contains(def))
                {
                    unlockedUpgrades.Add(otherDef);
                }
            }
            
            return unlockedUpgrades;
        }
        
        /// <summary>
        /// Gets installed upgrades that depend on the given upgrade.
        /// </summary>
        public static List<StaircaseUpgradeDef> GetInstalledUpgradesThatRequire(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> dependentUpgrades = new List<StaircaseUpgradeDef>();
            
            foreach (var installedUpgrade in comp.GetActiveUpgradeDefs())
            {
                if (installedUpgrade.requiredUpgrades != null && installedUpgrade.requiredUpgrades.Contains(def))
                {
                    dependentUpgrades.Add(installedUpgrade);
                }
            }
            
            return dependentUpgrades;
        }
        
        /// <summary>
        /// Checks if any active upgrade provides noise protection (removes sleep disturbed).
        /// </summary>
        public static bool HasNoiseProtection(CompStaircaseUpgrades comp)
        {
            foreach (var def in comp.GetActiveUpgradeDefs())
            {
                if (def.removeSleepDisturbed)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Gets the maximum hygiene capacity from installed bathroom upgrades.
        /// Returns the highest hygieneMaxCap value, or 0 if no bathroom upgrades.
        /// </summary>
        public static float GetMaxHygieneCapacity(CompStaircaseUpgrades comp)
        {
            float maxCap = 0f;
            
            foreach (var def in comp.GetActiveUpgradeDefs())
            {
                if (def.IsBathroomUpgrade && def.hygieneMaxCap > maxCap)
                {
                    maxCap = def.hygieneMaxCap;
                }
            }
            
            return maxCap;
        }
    }
}
