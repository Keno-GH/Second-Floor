using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;

namespace SecondFloor
{
    public static class DebugActions
    {
        [DebugAction("Second Floor", "List all active upgrades", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ListAllActiveUpgrades()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("No current map");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Active Upgrades on Current Map ===");
            sb.AppendLine();

            // Find all things with CompStaircaseUpgrades
            List<Thing> staircasesWithUpgrades = map.listerThings.AllThings
                .Where(t => t.TryGetComp<CompStaircaseUpgrades>() != null)
                .ToList();

            if (staircasesWithUpgrades.Count == 0)
            {
                sb.AppendLine("No staircases with upgrades found on this map.");
                Log.Message(sb.ToString());
                return;
            }

            int totalUpgrades = 0;
            foreach (Thing thing in staircasesWithUpgrades)
            {
                CompStaircaseUpgrades comp = thing.TryGetComp<CompStaircaseUpgrades>();
                if (comp == null || comp.activeUpgrades == null || comp.activeUpgrades.Count == 0)
                {
                    continue;
                }

                sb.AppendLine($"--- {thing.def.label} at {thing.Position} ---");
                sb.AppendLine($"  Space: {comp.GetUsedSpace()}/{comp.GetTotalSpace()}");
                sb.AppendLine($"  Upgrades ({comp.activeUpgrades.Count}):");
                
                foreach (var activeUpgrade in comp.activeUpgrades)
                {
                    totalUpgrades++;
                    string stuffInfo = activeUpgrade.stuff != null ? $" (Stuff: {activeUpgrade.stuff.label})" : "";
                    sb.AppendLine($"    - {activeUpgrade.def.label}{stuffInfo}");
                    sb.AppendLine($"      Space Cost: {activeUpgrade.def.spaceCost}");
                    
                    if (activeUpgrade.def.bedCountOffset != 0)
                    {
                        sb.AppendLine($"      Bed Count Offset: {activeUpgrade.def.bedCountOffset:+#;-#;0}");
                    }
                    if (activeUpgrade.def.bedCountMultiplier != 1f)
                    {
                        sb.AppendLine($"      Bed Count Multiplier: {activeUpgrade.def.bedCountMultiplier}x");
                    }
                    if (activeUpgrade.def.impressivenessLevel > 0)
                    {
                        sb.AppendLine($"      Impressiveness Level: {activeUpgrade.def.impressivenessLevel}");
                    }
                    if (activeUpgrade.def.removeSleepDisturbed)
                    {
                        sb.AppendLine($"      Removes Sleep Disturbed: Yes");
                    }
                    if (activeUpgrade.def.minConstructionSkill > 0)
                    {
                        sb.AppendLine($"      Min Construction Skill: {activeUpgrade.def.minConstructionSkill}");
                    }
                    if (activeUpgrade.def.minArtisticSkill > 0)
                    {
                        sb.AppendLine($"      Min Artistic Skill: {activeUpgrade.def.minArtisticSkill}");
                    }
                    if (activeUpgrade.def.RequiresConstruction)
                    {
                        sb.AppendLine($"      Requires Construction: Yes");
                    }
                    if (activeUpgrade.def.upgradeBuildingDef != null)
                    {
                        sb.AppendLine($"      Upgrade Building Def: {activeUpgrade.def.upgradeBuildingDef.label}");
                    }
                    if (activeUpgrade.def.upgradeBuildingDef != null && activeUpgrade.def.costList != null && activeUpgrade.def.costList.Count > 0)
                    {
                        sb.AppendLine($"      Custom Cost List:");
                        foreach (var cost in activeUpgrade.def.costList)
                        {
                            sb.AppendLine($"        - {cost.count} x {cost.thingDef.label}");
                        }
                    }
                    if (activeUpgrade.def.upgradeBuildingDef != null && activeUpgrade.def.stuffCategories != null && activeUpgrade.def.stuffCategories.Count > 0)
                    {
                        sb.AppendLine($"      Allowed Stuff Categories:");
                        foreach (var cat in activeUpgrade.def.stuffCategories)
                        {
                            sb.AppendLine($"        - {cat.label}");
                        }
                    }
                    if (activeUpgrade.def.upgradeBuildingDef != null && activeUpgrade.def.upgradeBuildingDef.costStuffCount > 0)
                    {
                        sb.AppendLine($"      Upgrade Building Stuff Cost: {activeUpgrade.def.upgradeBuildingDef.costStuffCount}");
                    }
                    if (activeUpgrade.def.upgradeBuildingDef != null && activeUpgrade.def.workToBuild > 0f)
                    {
                        sb.AppendLine($"      Custom Work To Build: {activeUpgrade.def.workToBuild}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Total staircases with upgrades: {staircasesWithUpgrades.Count(t => t.TryGetComp<CompStaircaseUpgrades>()?.activeUpgrades?.Count > 0)}");
            sb.AppendLine($"Total upgrades installed: {totalUpgrades}");

            Log.Message(sb.ToString());
        }

        [DebugAction("Second Floor", "List upgrade stats summary", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ListUpgradeStatsSummary()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("No current map");
                return;
            }

            // Count upgrades by type
            Dictionary<StaircaseUpgradeDef, int> upgradeCount = new Dictionary<StaircaseUpgradeDef, int>();
            Dictionary<StaircaseUpgradeDef, Dictionary<ThingDef, int>> upgradeStuffCount = new Dictionary<StaircaseUpgradeDef, Dictionary<ThingDef, int>>();

            List<Thing> staircasesWithUpgrades = map.listerThings.AllThings
                .Where(t => t.TryGetComp<CompStaircaseUpgrades>() != null)
                .ToList();

            foreach (Thing thing in staircasesWithUpgrades)
            {
                CompStaircaseUpgrades comp = thing.TryGetComp<CompStaircaseUpgrades>();
                if (comp == null || comp.activeUpgrades == null)
                {
                    continue;
                }

                foreach (var activeUpgrade in comp.activeUpgrades)
                {
                    if (!upgradeCount.ContainsKey(activeUpgrade.def))
                    {
                        upgradeCount[activeUpgrade.def] = 0;
                        upgradeStuffCount[activeUpgrade.def] = new Dictionary<ThingDef, int>();
                    }

                    upgradeCount[activeUpgrade.def]++;

                    if (activeUpgrade.stuff != null)
                    {
                        if (!upgradeStuffCount[activeUpgrade.def].ContainsKey(activeUpgrade.stuff))
                        {
                            upgradeStuffCount[activeUpgrade.def][activeUpgrade.stuff] = 0;
                        }
                        upgradeStuffCount[activeUpgrade.def][activeUpgrade.stuff]++;
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Upgrade Summary ===");
            sb.AppendLine();

            if (upgradeCount.Count == 0)
            {
                sb.AppendLine("No upgrades found on this map.");
                Log.Message(sb.ToString());
                return;
            }

            foreach (var kvp in upgradeCount.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"{kvp.Key.label}: {kvp.Value}");
                
                if (upgradeStuffCount[kvp.Key].Count > 0)
                {
                    foreach (var stuffKvp in upgradeStuffCount[kvp.Key].OrderByDescending(x => x.Value))
                    {
                        sb.AppendLine($"  - {stuffKvp.Key.label}: {stuffKvp.Value}");
                    }
                }
            }

            Log.Message(sb.ToString());
        }

        [DebugAction("Second Floor", "Inspect staircase at cell", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InspectStaircaseAtCell()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();

            if (!cell.InBounds(map))
            {
                return;
            }

            // Find staircases at this cell
            List<Thing> things = cell.GetThingList(map);
            Thing staircase = things.FirstOrDefault(t => t.TryGetComp<CompStaircaseUpgrades>() != null);

            if (staircase == null)
            {
                Messages.Message($"No staircase at {cell}", MessageTypeDefOf.RejectInput, false);
                return;
            }

            CompStaircaseUpgrades comp = staircase.TryGetComp<CompStaircaseUpgrades>();
            if (comp == null)
            {
                Messages.Message($"Staircase has no upgrade comp", MessageTypeDefOf.RejectInput, false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {staircase.def.label} at {cell} ===");
            sb.AppendLine($"Space: {comp.GetUsedSpace()}/{comp.GetTotalSpace()}");
            sb.AppendLine($"Active Upgrades: {comp.activeUpgrades?.Count ?? 0}");
            sb.AppendLine();

            if (comp.activeUpgrades != null && comp.activeUpgrades.Count > 0)
            {
                foreach (var activeUpgrade in comp.activeUpgrades)
                {
                    string stuffInfo = activeUpgrade.stuff != null ? $" ({activeUpgrade.stuff.label})" : "";
                    sb.AppendLine($"- {activeUpgrade.def.label}{stuffInfo}");
                }
            }
            else
            {
                sb.AppendLine("No upgrades installed.");
            }

            Messages.Message(sb.ToString().TrimEnd(), MessageTypeDefOf.NeutralEvent, false);
            Log.Message(sb.ToString());
        }
    }
}
