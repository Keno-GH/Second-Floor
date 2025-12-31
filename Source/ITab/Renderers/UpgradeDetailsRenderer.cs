using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// Renders the upgrade details panel (right side) showing effects, costs, requirements, and actions.
    /// </summary>
    public static class UpgradeDetailsRenderer
    {
        private static Vector2 detailsScrollPosition = Vector2.zero;
        private static float detailsScrollHeight = 0f;
        
        /// <summary>
        /// Draws the upgrade details panel for the selected upgrade.
        /// </summary>
        public static void Draw(Rect rect, Thing staircase, CompStaircaseUpgrades comp, 
            StaircaseUpgradeDef def, ref StaircaseUpgradeDef selectedUpgrade)
        {
            if (def == null)
            {
                return;
            }
            
            // Draw background
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(8f);
            
            bool isInstalled = comp.HasActiveUpgrade(def);
            bool isPending = UpgradeFiltering.GetPendingUpgrades(staircase).Contains(def);
            bool isConstructed = comp.HasUpgrade(def);
            bool isLocked = UpgradeFiltering.IsUpgradeLocked(def, comp);
            
            UpgradeDisableReason disableReason = UpgradeDisableReason.None;
            if (isConstructed && !isInstalled)
            {
                disableReason = comp.GetUpgradeDisableReason(def);
            }
            
            // Reserve space for buttons at bottom
            Rect scrollableRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 
                innerRect.height - TabLayout.ButtonHeight - TabLayout.ContentPadding);
            Rect buttonRect = new Rect(innerRect.x, scrollableRect.yMax + TabLayout.ContentPadding, 
                innerRect.width, TabLayout.ButtonHeight);
            
            // Scrollable content area
            Rect viewRect = new Rect(0f, 0f, scrollableRect.width - 16f, detailsScrollHeight);
            Widgets.BeginScrollView(scrollableRect, ref detailsScrollPosition, viewRect);
            
            float curY = 0f;
            
            // Title
            Rect titleRect = new Rect(0f, curY, viewRect.width, 28f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, def.label);
            Text.Font = GameFont.Small;
            curY += 28f + 6f;
            
            // Status indicator
            curY = DrawStatusSection(curY, viewRect.width, def, comp, isInstalled, isPending, isConstructed, disableReason);
            
            // Description
            float descHeight = Text.CalcHeight(def.description, viewRect.width);
            Rect descRect = new Rect(0f, curY, viewRect.width, descHeight);
            Widgets.Label(descRect, def.description);
            curY += descHeight + TabLayout.SectionSpacing;
            
            // Effects section
            curY = DrawEffectsSection(curY, viewRect.width, def);
            
            // Costs section
            curY = DrawCostsSection(curY, viewRect.width, def, comp, staircase);
            
            // Requirements section
            curY = DrawRequirementsSection(curY, viewRect.width, def, comp, staircase);
            
            // Unlocks section
            curY = DrawUnlocksSection(curY, viewRect.width, def);
            
            // Store height for next frame
            detailsScrollHeight = curY;
            
            Widgets.EndScrollView();
            
            // Buttons at the bottom
            DrawActionButtons(buttonRect, def, comp, staircase, isInstalled, isPending, isConstructed, 
                isLocked, disableReason, ref selectedUpgrade);
        }
        
        private static float DrawStatusSection(float curY, float width, StaircaseUpgradeDef def, 
            CompStaircaseUpgrades comp, bool isInstalled, bool isPending, bool isConstructed, 
            UpgradeDisableReason disableReason)
        {
            string status = "";
            Color statusColor = Color.white;
            
            if (disableReason != UpgradeDisableReason.None)
            {
                status = "DISABLED: ";
                switch (disableReason)
                {
                    case UpgradeDisableReason.ToggledOff:
                        status += "Toggled Off";
                        break;
                    case UpgradeDisableReason.OutOfFuel:
                        status += "Out of Fuel";
                        break;
                    case UpgradeDisableReason.NoPower:
                        status += "No Power";
                        break;
                    case UpgradeDisableReason.InsufficientCount:
                        status += "Not Enough Constructed";
                        break;
                    default:
                        status += "Unknown Reason";
                        break;
                }
                statusColor = new Color(1f, 0.5f, 0f);
            }
            else if (isInstalled)
            {
                status = "INSTALLED";
                statusColor = Color.green;
            }
            else if (isPending)
            {
                status = "UNDER CONSTRUCTION";
                statusColor = Color.yellow;
            }
            else
            {
                status = "NOT INSTALLED";
                statusColor = Color.gray;
            }
            
            // Add stuff material to status if applicable
            if ((isInstalled || disableReason != UpgradeDisableReason.None) && def.RequiresConstruction && 
                def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
            {
                ActiveUpgrade activeUpgrade = comp.constructedUpgrades.FirstOrDefault(au => au.def == def);
                if (activeUpgrade != null && activeUpgrade.stuff != null)
                {
                    status += $" ({activeUpgrade.stuff.LabelCap})";
                }
            }
            
            GUI.color = statusColor;
            Rect statusRect = new Rect(0f, curY, width, TabLayout.StatsLineHeight);
            Widgets.Label(statusRect, $"Status: {status}");
            GUI.color = Color.white;
            
            return curY + TabLayout.StatsLineHeight + 6f;
        }
        
        private static float DrawEffectsSection(float curY, float width, StaircaseUpgradeDef def)
        {
            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "<b>Effects:</b>");
            curY += TabLayout.StatsLineHeight;
            
            bool hasEffects = false;
            
            if (def.bedCountOffset != 0)
            {
                string sign = def.bedCountOffset > 0 ? "+" : "";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Bed count: {sign}{def.bedCountOffset}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.bedCountMultiplier != 1f)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Bed count multiplier: x{def.bedCountMultiplier}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.removeSleepDisturbed)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "  Removes sleep disturbed");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.impressivenessLevel != 0)
            {
                string sign = def.impressivenessLevel > 0 ? "+" : "";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Impressiveness: {sign}{def.impressivenessLevel} level(s)");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.thoughtReplacement != null)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "  Changes room type thought");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.comfortBonus != 0f)
            {
                string sign = def.comfortBonus > 0 ? "+" : "";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Comfort: {sign}{def.comfortBonus.ToStringPercent()}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.sleepEffectivenessBonus != 0f)
            {
                string sign = def.sleepEffectivenessBonus > 0 ? "+" : "";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Sleep Effectiveness: {sign}{def.sleepEffectivenessBonus.ToStringPercent()}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            // Temperature effects
            if (def.heatOffset > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Heating: +{def.heatOffset.ToStringTemperature("F1")}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.maxHeatCap < 100f)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Heats up to: {def.maxHeatCap.ToStringTemperature("F0")}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.coolOffset > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Cooling: -{def.coolOffset.ToStringTemperature("F1")}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.minCoolCap > -273f)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Cools down to: {def.minCoolCap.ToStringTemperature("F0")}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.insulationAdjustment > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                    $"  Insulation: {def.insulationAdjustment.ToStringTemperature("F1")} towards {def.insulationTarget.ToStringTemperature("F0")}");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (def.fuelPerBed > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Fuel Consumption: {def.fuelPerBed:F1} per bed per day");
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            // Smart temperature modifiers
            if (def.IsSmartTempModifier)
            {
                if (def.smartTempModifierType == TempModifierType.HeaterOnly && def.smartHeatEfficiency > 0)
                {
                    float maxHeat = (def.basePowerConsumption / 100f) * def.smartHeatEfficiency;
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Smart Heating: up to +{maxHeat.ToStringTemperature("F1")}");
                    curY += TabLayout.StatsLineHeight;
                    hasEffects = true;
                }
                else if (def.smartTempModifierType == TempModifierType.CoolerOnly && def.smartCoolEfficiency > 0)
                {
                    float maxCool = (def.basePowerConsumption / 100f) * def.smartCoolEfficiency;
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Smart Cooling: up to -{maxCool.ToStringTemperature("F1")}");
                    curY += TabLayout.StatsLineHeight;
                    hasEffects = true;
                }
                else if (def.smartTempModifierType == TempModifierType.DualMode)
                {
                    if (def.smartHeatEfficiency > 0)
                    {
                        float maxHeat = (def.basePowerConsumption / 100f) * def.smartHeatEfficiency;
                        Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Smart Heating: up to +{maxHeat.ToStringTemperature("F1")}");
                        curY += TabLayout.StatsLineHeight;
                        hasEffects = true;
                    }
                    if (def.smartCoolEfficiency > 0)
                    {
                        float maxCool = (def.basePowerConsumption / 100f) * def.smartCoolEfficiency;
                        Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Smart Cooling: up to -{maxCool.ToStringTemperature("F1")}");
                        curY += TabLayout.StatsLineHeight;
                        hasEffects = true;
                    }
                }
            }
            
            if (def.requiresPower && def.basePowerConsumption > 0)
            {
                var ext = def.upgradeBuildingDef?.GetModExtension<StaircaseUpgradeExtension>();
                bool isOnePerBed = ext?.onePerBed == true;
                bool directlyToBed = ext?.directlyToBed ?? false;
                if (isOnePerBed)
                {
                    if (directlyToBed)
                    {
                        Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                            "SF_PowerUsagePerBed".Translate(def.basePowerConsumption.ToString("F0")));
                    }
                    else
                    {
                        Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                            "SF_PowerUsagePerBedAmbient".Translate(def.basePowerConsumption.ToString("F0")));
                    }
                }
                else
                {
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  Power Usage: {def.basePowerConsumption:F0}W");
                }
                curY += TabLayout.StatsLineHeight;
                hasEffects = true;
            }
            
            if (!hasEffects)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "  No gameplay effects");
                curY += TabLayout.StatsLineHeight;
            }
            
            return curY + TabLayout.SectionSpacing;
        }
        
        private static float DrawCostsSection(float curY, float width, StaircaseUpgradeDef def, 
            CompStaircaseUpgrades comp, Thing staircase)
        {
            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "<b>Costs:</b>");
            curY += TabLayout.StatsLineHeight;
            
            // Calculate effective bed count
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int rawBedCount = bedsComp?.bedCount ?? 1;
            var costExt = def.upgradeBuildingDef?.GetModExtension<StaircaseUpgradeExtension>();
            bool costIsOnePerBed = costExt?.onePerBed ?? true;
            bool costIsDirectlyToBed = costExt?.directlyToBed ?? false;
            bool isBarracksRoom = comp.IsBarracks;
            
            int effectiveBedCount = rawBedCount;
            bool costsAreHalved = false;
            if (costIsOnePerBed && !costIsDirectlyToBed && isBarracksRoom && rawBedCount > 1)
            {
                effectiveBedCount = Mathf.CeilToInt(rawBedCount / 2f);
                costsAreHalved = true;
            }
            else if (!costIsOnePerBed)
            {
                effectiveBedCount = 1;
            }
            
            // Show barracks note if applicable
            if (isBarracksRoom && costIsOnePerBed)
            {
                if (costIsDirectlyToBed)
                {
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                        "<color=#aaaaaa>* Since this upgrade affects beds directly, it follows normal costs</color>");
                    curY += TabLayout.StatsLineHeight;
                }
                else if (costsAreHalved)
                {
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                        "<color=#aaaaaa>* These costs are halved for barracks</color>");
                    curY += TabLayout.StatsLineHeight;
                }
            }
            
            // Space cost
            float baseSpaceCost = def.spaceCost;
            float perBedSpaceCost = def.spaceCostPerBed;
            float totalSpaceCost = comp.GetRequiredSpaceForUpgrade(def);
            float spaceAvailable = comp.GetTotalSpace() - comp.GetUsedSpace();
            string spaceColorTag = spaceAvailable >= totalSpaceCost ? "" : "<color=#ff6666>";
            string spaceColorEnd = spaceAvailable >= totalSpaceCost ? "" : "</color>";
            
            if (costIsOnePerBed && perBedSpaceCost > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                    $"  {spaceColorTag}Space: {totalSpaceCost} ({perBedSpaceCost} per bed + {baseSpaceCost}){spaceColorEnd}");
            }
            else
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                    $"  {spaceColorTag}Space: {totalSpaceCost}{spaceColorEnd}");
            }
            curY += TabLayout.StatsLineHeight;
            
            // Additional space from bed increase
            if (def.bedCountOffset > 0 || def.bedCountMultiplier > 1f)
            {
                var additionalSpaceBreakdown = comp.GetAdditionalSpaceCostBreakdown(def);
                if (additionalSpaceBreakdown.Count > 0)
                {
                    float totalAdditionalSpace = additionalSpaceBreakdown.Sum(x => x.additionalSpace);
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                        $"  <color=#ffcc00>Additional space from installed upgrades: +{totalAdditionalSpace}</color>");
                    curY += TabLayout.StatsLineHeight;
                    foreach (var (upgradeLabel, additionalSpace) in additionalSpaceBreakdown)
                    {
                        Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                            $"    • {upgradeLabel}: +{additionalSpace}");
                        curY += TabLayout.StatsLineHeight;
                    }
                }
            }
            
            // Material costs
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                ThingDef buildingDef = def.upgradeBuildingDef;
                
                if (buildingDef.costStuffCount > 0)
                {
                    string stuffCategoryLabel = "material";
                    if (def.stuffCategories != null && def.stuffCategories.Count > 0)
                    {
                        stuffCategoryLabel = string.Join(", ", def.stuffCategories.Select(sc => sc.label));
                    }
                    
                    int perBedStuffCost = buildingDef.costStuffCount;
                    int totalStuffCost = perBedStuffCost * effectiveBedCount;
                    
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                        $"  Stuffable Materials: {totalStuffCost} ({perBedStuffCost} per bed) {stuffCategoryLabel}");
                    curY += TabLayout.StatsLineHeight;
                }
                
                if (buildingDef.costList != null && buildingDef.costList.Count > 0)
                {
                    foreach (var cost in buildingDef.costList)
                    {
                        int perBedCost = cost.count;
                        int totalCost = perBedCost * effectiveBedCount;
                        int available = staircase.Map.resourceCounter.GetCount(cost.thingDef);
                        string colorTag = available >= totalCost ? "" : "<color=#ff6666>";
                        string colorEnd = available >= totalCost ? "" : "</color>";
                        
                        if (costIsOnePerBed && effectiveBedCount > 1)
                        {
                            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                                $"  {colorTag}{cost.thingDef.LabelCap}: {totalCost} ({perBedCost} per bed){colorEnd}");
                        }
                        else
                        {
                            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                                $"  {colorTag}{cost.thingDef.LabelCap}: {totalCost}{colorEnd}");
                        }
                        curY += TabLayout.StatsLineHeight;
                    }
                }
            }
            
            return curY + TabLayout.SectionSpacing;
        }
        
        private static float DrawRequirementsSection(float curY, float width, StaircaseUpgradeDef def, 
            CompStaircaseUpgrades comp, Thing staircase)
        {
            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "<b>Requirements:</b>");
            curY += TabLayout.StatsLineHeight;
            
            bool hasRequirements = false;
            
            // Required upgrades
            if (def.requiredUpgrades != null && def.requiredUpgrades.Count > 0)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "  Required upgrades:");
                curY += TabLayout.StatsLineHeight;
                
                foreach (var requiredUpgrade in def.requiredUpgrades)
                {
                    bool reqInstalled = comp.HasActiveUpgrade(requiredUpgrade);
                    string colorTag = reqInstalled ? "<color=#00ff00>" : "<color=#ff6666>";
                    string colorEnd = "</color>";
                    string reqStatus = reqInstalled ? "✓" : "✗";
                    Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                        $"    {colorTag}{reqStatus} {requiredUpgrade.label}{colorEnd}");
                    curY += TabLayout.StatsLineHeight;
                }
                hasRequirements = true;
            }
            
            if (def.minConstructionSkill > 0)
            {
                bool hasSkilledPawn = staircase.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Construction).Level >= def.minConstructionSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                    $"  {colorTag}Construction skill: {def.minConstructionSkill}{colorEnd}");
                curY += TabLayout.StatsLineHeight;
                hasRequirements = true;
            }
            
            if (def.minArtisticSkill > 0)
            {
                bool hasSkilledPawn = staircase.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Artistic).Level >= def.minArtisticSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), 
                    $"  {colorTag}Artistic skill: {def.minArtisticSkill}{colorEnd}");
                curY += TabLayout.StatsLineHeight;
                hasRequirements = true;
            }
            
            if (!hasRequirements)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "  None");
                curY += TabLayout.StatsLineHeight;
            }
            
            return curY + TabLayout.SectionSpacing;
        }
        
        private static float DrawUnlocksSection(float curY, float width, StaircaseUpgradeDef def)
        {
            List<StaircaseUpgradeDef> unlocksUpgrades = UpgradeFiltering.GetUpgradesUnlockedBy(def);
            
            if (unlocksUpgrades.Count == 0)
            {
                return curY;
            }
            
            Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), "<b>Unlocks:</b>");
            curY += TabLayout.StatsLineHeight;
            
            foreach (var unlockedUpgrade in unlocksUpgrades)
            {
                Widgets.Label(new Rect(0f, curY, width, TabLayout.StatsLineHeight), $"  • {unlockedUpgrade.label}");
                curY += TabLayout.StatsLineHeight;
            }
            
            return curY + TabLayout.SectionSpacing;
        }
        
        private static void DrawActionButtons(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, bool isInstalled, bool isPending, bool isConstructed, bool isLocked, 
            UpgradeDisableReason disableReason, ref StaircaseUpgradeDef selectedUpgrade)
        {
            // The button logic is complex - handle different states
            
            if (isInstalled)
            {
                DrawInstalledButtons(rect, def, comp, staircase, ref selectedUpgrade);
            }
            else if (disableReason == UpgradeDisableReason.ToggledOff)
            {
                DrawToggledOffButtons(rect, def, comp, staircase, ref selectedUpgrade);
            }
            else if (disableReason == UpgradeDisableReason.InsufficientCount)
            {
                DrawInsufficientCountButtons(rect, def, comp, staircase);
            }
            else if (disableReason != UpgradeDisableReason.None)
            {
                DrawDisabledButtons(rect, def, comp, staircase, ref selectedUpgrade);
            }
            else if (isPending)
            {
                DrawPendingButtons(rect, def, staircase);
            }
            else
            {
                DrawBuildButtons(rect, def, comp, staircase, isLocked);
            }
        }
        
        private static void DrawInstalledButtons(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, ref StaircaseUpgradeDef selectedUpgrade)
        {
            var ext = def.upgradeBuildingDef?.GetModExtension<StaircaseUpgradeExtension>();
            bool isOnePerBed = def.RequiresConstruction && (ext?.onePerBed == true);
            int constructedCount = comp.GetConstructedCount(def);
            int requiredCount = comp.GetRequiredBedCountForUpgrade(def);
            int excessCount = isOnePerBed ? constructedCount - requiredCount : 0;
            
            if (excessCount > 0)
            {
                // Has excess - show Remove Excess button
                if (def.CanBeToggled)
                {
                    float buttonWidth = (rect.width - 10f) / 3f;
                    Rect toggleRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                    Rect excessRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                    Rect removeRect = new Rect(rect.x + buttonWidth * 2 + 10f, rect.y, buttonWidth, rect.height);
                    
                    if (Widgets.ButtonText(toggleRect, "SF_ToggleOff".Translate()))
                    {
                        comp.ToggleUpgrade(def);
                    }
                    
                    GUI.color = new Color(1f, 0.8f, 0.5f);
                    if (Widgets.ButtonText(excessRect, $"Remove {excessCount} Excess"))
                    {
                        UpgradeActions.TryRemoveExcessUpgrades(def, comp, staircase, excessCount);
                    }
                    GUI.color = Color.white;
                    
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (Widgets.ButtonText(removeRect, "Remove All"))
                    {
                        UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    float buttonWidth = (rect.width - 5f) / 2f;
                    Rect excessRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                    Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                    
                    GUI.color = new Color(1f, 0.8f, 0.5f);
                    if (Widgets.ButtonText(excessRect, $"Remove {excessCount} Excess"))
                    {
                        UpgradeActions.TryRemoveExcessUpgrades(def, comp, staircase, excessCount);
                    }
                    GUI.color = Color.white;
                    
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (Widgets.ButtonText(removeRect, "Remove All"))
                    {
                        UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                    }
                    GUI.color = Color.white;
                }
            }
            else if (def.CanBeToggled)
            {
                float buttonWidth = (rect.width - 5f) / 2f;
                Rect toggleRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                
                if (Widgets.ButtonText(toggleRect, "SF_ToggleOff".Translate()))
                {
                    comp.ToggleUpgrade(def);
                }
                
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(removeRect, "Remove (75% refund)"))
                {
                    UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                }
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(rect, "Remove (75% refund)"))
                {
                    UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                }
            }
        }
        
        private static void DrawToggledOffButtons(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, ref StaircaseUpgradeDef selectedUpgrade)
        {
            float buttonWidth = (rect.width - 5f) / 2f;
            Rect toggleRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
            
            GUI.color = Color.green;
            if (Widgets.ButtonText(toggleRect, "SF_ToggleOn".Translate()))
            {
                comp.ToggleUpgrade(def);
            }
            GUI.color = Color.white;
            
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(removeRect, "Remove (75% refund)"))
            {
                UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
            }
            GUI.color = Color.white;
        }
        
        private static void DrawInsufficientCountButtons(Rect rect, StaircaseUpgradeDef def, 
            CompStaircaseUpgrades comp, Thing staircase)
        {
            int constructedCount = comp.GetConstructedCount(def);
            int requiredCount = comp.GetRequiredBedCountForUpgrade(def);
            int pendingCount = UpgradeFiltering.GetPendingUpgradeCount(staircase, def);
            int needed = requiredCount - constructedCount - pendingCount;
            bool allPending = needed <= 0 && pendingCount > 0;
            
            if (Prefs.DevMode)
            {
                float buttonWidth = (rect.width - 10f) / 3f;
                Rect addRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                Rect devRect = new Rect(rect.x + buttonWidth * 2 + 10f, rect.y, buttonWidth, rect.height);
                
                string addLabel = allPending ? $"{pendingCount} Pending" : $"Add {needed} More";
                if (allPending) GUI.color = Color.gray;
                if (Widgets.ButtonText(addRect, addLabel, active: !allPending))
                {
                    if (!allPending) UpgradeActions.FillMissingBlueprints(def, comp, staircase);
                }
                GUI.color = Color.white;
                
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(removeRect, $"Remove {constructedCount}"))
                {
                    UpgradeActions.TryRemoveConstructedUpgrades(def, comp, staircase);
                }
                GUI.color = Color.white;
                
                GUI.color = new Color(0.8f, 0.5f, 1f);
                if (Widgets.ButtonText(devRect, "DEV: Instant"))
                {
                    UpgradeActions.DevModeInstantUpgrade(def, comp, staircase);
                }
                GUI.color = Color.white;
            }
            else
            {
                float buttonWidth = (rect.width - 5f) / 2f;
                Rect addRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                
                string addLabel = allPending ? $"{pendingCount} Pending" : $"Add {needed} More";
                if (allPending) GUI.color = Color.gray;
                if (Widgets.ButtonText(addRect, addLabel, active: !allPending))
                {
                    if (!allPending) UpgradeActions.FillMissingBlueprints(def, comp, staircase);
                }
                GUI.color = Color.white;
                
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(removeRect, $"Remove {constructedCount}"))
                {
                    UpgradeActions.TryRemoveConstructedUpgrades(def, comp, staircase);
                }
                GUI.color = Color.white;
            }
        }
        
        private static void DrawDisabledButtons(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, ref StaircaseUpgradeDef selectedUpgrade)
        {
            if (def.CanBeToggled)
            {
                float buttonWidth = (rect.width - 5f) / 2f;
                Rect toggleRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect removeRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                
                if (Widgets.ButtonText(toggleRect, "SF_ToggleOff".Translate()))
                {
                    comp.ToggleUpgrade(def);
                }
                
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(removeRect, "Remove (75% refund)"))
                {
                    UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                }
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(rect, "Remove (75% refund)"))
                {
                    UpgradeActions.TryRemoveUpgrade(def, comp, staircase, ref selectedUpgrade);
                }
            }
        }
        
        private static void DrawPendingButtons(Rect rect, StaircaseUpgradeDef def, Thing staircase)
        {
            if (Widgets.ButtonText(rect, "Cancel Construction"))
            {
                UpgradeActions.TryCancelUpgrade(def, staircase);
            }
        }
        
        private static void DrawBuildButtons(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, bool isLocked)
        {
            float availableSpace = comp.GetTotalSpace() - comp.GetUsedSpace();
            float requiredSpace = comp.GetRequiredSpaceForUpgrade(def);
            bool canAffordSpace = availableSpace >= requiredSpace;
            bool canBuild = canAffordSpace && !isLocked;
            
            if (Prefs.DevMode)
            {
                float buttonWidth = (rect.width - 5f) / 2f;
                Rect buildRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect devRect = new Rect(rect.x + buttonWidth + 5f, rect.y, buttonWidth, rect.height);
                
                Color oldColor = GUI.color;
                if (!canBuild) GUI.color = Color.red;
                if (Widgets.ButtonText(buildRect, "Build Upgrade", active: canBuild))
                {
                    if (canBuild) UpgradeActions.TryAddUpgrade(def, comp, staircase);
                }
                GUI.color = oldColor;
                
                if (isLocked)
                {
                    TooltipHandler.TipRegion(buildRect, "This upgrade is locked. Install the required upgrades first.");
                }
                else if (!canAffordSpace)
                {
                    TooltipHandler.TipRegion(buildRect, $"Not enough space! Need {requiredSpace}, have {availableSpace}");
                }
                
                GUI.color = new Color(0.8f, 0.5f, 1f);
                if (Widgets.ButtonText(devRect, "DEV: Instant"))
                {
                    UpgradeActions.DevModeInstantUpgrade(def, comp, staircase);
                }
                GUI.color = Color.white;
            }
            else
            {
                Color oldColor = GUI.color;
                if (!canBuild) GUI.color = Color.red;
                if (Widgets.ButtonText(rect, "Build Upgrade", active: canBuild))
                {
                    if (canBuild) UpgradeActions.TryAddUpgrade(def, comp, staircase);
                }
                GUI.color = oldColor;
                
                if (isLocked)
                {
                    TooltipHandler.TipRegion(rect, "This upgrade is locked. Install the required upgrades first.");
                }
                else if (!canAffordSpace)
                {
                    TooltipHandler.TipRegion(rect, $"Not enough space! Need {requiredSpace}, have {availableSpace}");
                }
            }
        }
    }
}
