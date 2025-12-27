using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecondFloor
{
    public class ITab_Staircase : ITab
    {
        // Main window size - wider to accommodate the two-panel layout
        private static readonly Vector2 WinSize = new Vector2(600f, 520f);
        
        // Left panel (upgrade list) dimensions
        private const float LeftPanelWidth = 220f;
        private const float UpgradeRowHeight = 40f;
        private const float IconSize = 32f;
        
        // Right panel (details) dimensions  
        private const float RightPanelMargin = 10f;
        
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 detailsScrollPosition = Vector2.zero;
        private float detailsScrollHeight = 0f;
        private StaircaseUpgradeDef selectedUpgrade = null;

        // Cached textures for upgrade icons (will be loaded dynamically or use defaults)
        private static Texture2D defaultUpgradeIcon;
        private static Texture2D installedCheckmark;

        public ITab_Staircase()
        {
            this.size = WinSize;
            this.labelKey = "TabStaircase";
            this.tutorTag = "Staircase";
        }

        private static void LoadTextures()
        {
            if (defaultUpgradeIcon == null)
            {
                // Use a built-in texture as fallback
                defaultUpgradeIcon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false) ?? BaseContent.BadTex;
            }
            if (installedCheckmark == null)
            {
                installedCheckmark = ContentFinder<Texture2D>.Get("UI/Widgets/CheckOn", false) ?? BaseContent.BadTex;
            }
        }

        protected override void FillTab()
        {
            LoadTextures();
            
            CompStaircaseUpgrades comp = SelThing.TryGetComp<CompStaircaseUpgrades>();
            if (comp == null)
            {
                return;
            }

            Rect mainRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            
            // Draw header
            Rect headerRect = new Rect(mainRect.x, mainRect.y, mainRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "Staircase Details");
            Text.Font = GameFont.Small;
            
            // Draw stats below header
            float statsY = headerRect.yMax + 5f;
            float statsHeight = CalculateStatsHeight(comp);
            Rect statsRect = new Rect(mainRect.x, statsY, mainRect.width, statsHeight);
            DrawStaircaseStats(statsRect, comp);
            
            // Main content area (below stats)
            float contentY = statsRect.yMax + 10f;
            Rect contentRect = new Rect(mainRect.x, contentY, mainRect.width, mainRect.yMax - contentY);
            
            // Left panel - upgrade list
            Rect leftPanel = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            DrawUpgradeList(leftPanel, comp);
            
            // Right panel - details (only if an upgrade is selected)
            if (selectedUpgrade != null)
            {
                Rect rightPanel = new Rect(leftPanel.xMax + RightPanelMargin, contentRect.y, 
                    contentRect.width - LeftPanelWidth - RightPanelMargin, contentRect.height);
                DrawUpgradeDetails(rightPanel, comp, selectedUpgrade);
            }
        }

        private void DrawStaircaseStats(Rect rect, CompStaircaseUpgrades comp)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            
            CompMultipleBeds bedsComp = SelThing.TryGetComp<CompMultipleBeds>();
            if (bedsComp != null)
            {
                listing.Label($"Bed Spaces: {bedsComp.bedCount}");
            }

            // Calculate impressiveness level and room type
            int impressivenessLevel = 1;
            string roomType = "Private Rooms";
            bool isBarracks = false;
            bool isBasement = false;

            CompProperties_GiveThoughtStairs thoughtComp = SelThing.TryGetComp<CompGiveThoughtStairs>()?.Props;
            if (thoughtComp != null && thoughtComp.thoughtDef != null)
            {
                isBasement = thoughtComp.thoughtDef.defName == "SF_Low_Quality_Basement";
            }

            int totalImpressivenessBonus = 0;
            foreach (var upgrade in comp.GetActiveUpgradeDefs())
            {
                totalImpressivenessBonus += upgrade.impressivenessLevel;
                if (upgrade.defName == "SF_StaircaseUpgrade_Barracks")
                {
                    isBarracks = true;
                }
            }
            impressivenessLevel = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);

            if (isBasement)
            {
                roomType = isBarracks ? "Basement Barracks" : "Basement";
            }
            else
            {
                if (isBarracks)
                {
                    roomType = "Barracks";
                }
                else if (bedsComp != null && bedsComp.bedCount >= 4)
                {
                    roomType = "Multiple Private Rooms";
                }
                else
                {
                    roomType = "Single Bedroom";
                }
            }

            string[] impressivenessLabels = new string[]
            {
                "Awful", "Dull", "Mediocre", "Decent", "Slightly Impressive",
                "Impressive", "Very Impressive", "Extremely Impressive",
                "Unbelievably Impressive", "Wondrously Impressive"
            };
            
            listing.Label($"Room Type: {roomType}");
            listing.Label($"Impressiveness: {impressivenessLabels[impressivenessLevel]}");
            
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            listing.Label($"Space: {usedSpace}/{totalSpace} (Available: {totalSpace - usedSpace})");
            
            // Temperature display with base temp breakdown
            float currentTemp = comp.CurrentVirtualTemperature;
            float baseTemp = comp.GetBaseTemperature();
            float insulatedTemp = comp.GetInsulatedTemperature();
            float outdoorTemp = SelThing.Map?.mapTemperature.OutdoorTemp ?? 21f;
            
            string tempLabel = $"Temperature: {currentTemp.ToStringTemperature("F0")}";
            tempLabel += $" (Outdoor: {outdoorTemp.ToStringTemperature("F0")}";
            if (comp.HasAnyInsulatingModifier())
            {
                tempLabel += $", Insulated: {insulatedTemp.ToStringTemperature("F0")}";
            }
            if (comp.HasAnyDumbTempModifier())
            {
                tempLabel += $", Passive: {baseTemp.ToStringTemperature("F0")}";
            }
            if (comp.HasAnySmartTempModifier())
            {
                tempLabel += $", Active: {currentTemp.ToStringTemperature("F0")}";
            }
            tempLabel += ")";
            listing.Label(tempLabel);
            
            // Target temperature slider for smart temp modifiers
            if (comp.HasAnySmartTempModifier())
            {
                Rect sliderRect = listing.GetRect(28f);
                Rect labelRect = new Rect(sliderRect.x, sliderRect.y, 120f, sliderRect.height);
                Rect actualSliderRect = new Rect(labelRect.xMax, sliderRect.y, sliderRect.width - 120f, sliderRect.height);
                
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, "SF_TargetTemp".Translate() + ": " + comp.targetTemperature.ToStringTemperature("F0"));
                Text.Anchor = TextAnchor.UpperLeft;
                
                // Temperature slider from -10°C to 40°C
                float newTarget = Widgets.HorizontalSlider(actualSliderRect, comp.targetTemperature, -10f, 40f, true, null, "SF_TempMin".Translate(), "SF_TempMax".Translate(), 1f);
                if (newTarget != comp.targetTemperature)
                {
                    comp.targetTemperature = newTarget;
                }
            }
            
            // Power display for power-requiring upgrades
            if (comp.HasAnyPowerRequiringUpgrade())
            {
                float powerUsage = comp.CurrentPowerConsumption;
                bool hasPower = comp.HasPower();
                string powerLabel = $"Power: {powerUsage:F0}W";
                if (!hasPower)
                {
                    powerLabel += " (No power)";
                    GUI.color = Color.red;
                }
                listing.Label(powerLabel);
                GUI.color = Color.white;
            }
            
            // Fuel display for fuel-requiring upgrades
            if (comp.HasAnyFuelRequiringUpgrade())
            {
                float fuelUsage = comp.CurrentFuelConsumption;
                bool hasFuel = comp.HasFuel();
                string fuelLabel = $"Fuel: {fuelUsage:F1}/day";
                if (!hasFuel)
                {
                    fuelLabel += " (No fuel)";
                    GUI.color = Color.red;
                }
                listing.Label(fuelLabel);
                GUI.color = Color.white;
            }
            
            listing.End();
        }

        private float CalculateStatsHeight(CompStaircaseUpgrades comp)
        {
            // Base height for standard stats (bed spaces, room type, impressiveness, space, temperature)
            float height = 24f * 5f; // 5 lines of basic info
            
            // Add height for target temperature slider if there are smart temp modifiers
            if (comp.HasAnySmartTempModifier())
            {
                height += 28f; // Slider height
            }
            
            // Add height for power display if there are power-requiring upgrades
            if (comp.HasAnyPowerRequiringUpgrade())
            {
                height += 24f; // Power line
            }
            
            // Add height for fuel display if there are fuel-requiring upgrades
            if (comp.HasAnyFuelRequiringUpgrade())
            {
                height += 24f; // Fuel line
            }
            
            return height;
        }

        private void DrawUpgradeList(Rect rect, CompStaircaseUpgrades comp)
        {
            // Draw background
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(4f);
            
            // Section label
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, "Upgrades");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, labelRect.yMax + 4f, rect.width, rect.height - labelRect.height - 8f);
            
            // Gather all applicable upgrades
            List<StaircaseUpgradeDef> allUpgrades = GetApplicableUpgrades(comp);
            List<StaircaseUpgradeDef> pendingUpgrades = GetPendingUpgrades(SelThing);
            
            float viewHeight = allUpgrades.Count * UpgradeRowHeight;
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var def in allUpgrades)
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, UpgradeRowHeight);
                bool isInstalled = comp.HasActiveUpgrade(def);
                bool isPending = pendingUpgrades.Contains(def);
                bool isSelected = selectedUpgrade == def;
                bool isLocked = IsUpgradeLocked(def, comp);
                bool isConstructed = comp.HasUpgrade(def);
                UpgradeDisableReason disableReason = UpgradeDisableReason.None;
                if (isConstructed && !isInstalled)
                {
                    disableReason = comp.GetUpgradeDisableReason(def);
                }
                
                DrawUpgradeRow(rowRect, def, isInstalled, isPending, isSelected, isLocked, disableReason);
                curY += UpgradeRowHeight;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawUpgradeRow(Rect rect, StaircaseUpgradeDef def, bool isInstalled, bool isPending, bool isSelected, bool isLocked, UpgradeDisableReason disableReason)
        {
            // Highlight if selected
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // Alternating background for readability
            if (!isSelected && !Mouse.IsOver(rect))
            {
                int index = DefDatabase<StaircaseUpgradeDef>.AllDefsListForReading.IndexOf(def);
                if (index % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rect);
                }
            }
            
            // Icon area
            Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            
            // Get icon for upgrade (use default if none available)
            Texture2D icon = GetUpgradeIcon(def);
            
            // Gray out locked or disabled upgrades
            bool isDisabled = disableReason != UpgradeDisableReason.None;
            if (isLocked || isDisabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            
            GUI.DrawTexture(iconRect, icon);
            
            // Draw installed checkmark overlay (only if truly active)
            if (isInstalled)
            {
                Rect checkRect = new Rect(iconRect.xMax - 12f, iconRect.y, 14f, 14f);
                GUI.color = Color.green;
                GUI.DrawTexture(checkRect, installedCheckmark);
                GUI.color = Color.white;
            }
            // Draw warning icon for disabled upgrades
            else if (isDisabled)
            {
                Rect warningRect = new Rect(iconRect.xMax - 12f, iconRect.y, 14f, 14f);
                GUI.color = new Color(1f, 0.5f, 0f); // Orange
                GUI.DrawTexture(warningRect, ContentFinder<Texture2D>.Get("UI/Icons/Warning", false) ?? BaseContent.BadTex);
                GUI.color = Color.white;
            }
            
            // Label
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, rect.width - IconSize - 14f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Color based on status
            if (isLocked)
            {
                GUI.color = Color.gray;
            }
            else if (isDisabled)
            {
                GUI.color = new Color(1f, 0.5f, 0f); // Orange for disabled
            }
            else if (isInstalled)
            {
                GUI.color = Color.green;
            }
            else if (isPending)
            {
                GUI.color = Color.yellow;
            }
            
            string label = def.label;
            if (isPending)
            {
                label += " (pending)";
            }
            else if (isDisabled)
            {
                label += " (disabled)";
            }
            else if (isLocked)
            {
                label += " (locked)";
            }
            
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Handle click (don't allow selecting locked upgrades for building, but allow viewing them)
            if (Widgets.ButtonInvisible(rect))
            {
                selectedUpgrade = def;
            }
            
            // Tooltip
            string tooltip = def.description;
            if (isLocked)
            {
                tooltip += "\n\n<color=#ff6666>This upgrade requires other upgrades to be installed first.</color>";
            }
            else if (isDisabled)
            {
                tooltip += "\n\n<color=#ffaa00>This upgrade is constructed but disabled: ";
                switch (disableReason)
                {
                    case UpgradeDisableReason.ToggledOff:
                        tooltip += "Toggled off</color>";
                        break;
                    case UpgradeDisableReason.OutOfFuel:
                        tooltip += "Out of fuel</color>";
                        break;
                    case UpgradeDisableReason.NoPower:
                        tooltip += "No power</color>";
                        break;
                    case UpgradeDisableReason.InsufficientCount:
                        tooltip += "Not enough constructed (requires one per bed)</color>";
                        break;
                    default:
                        tooltip += "Unknown reason</color>";
                        break;
                }
            }
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private void DrawUpgradeDetails(Rect rect, CompStaircaseUpgrades comp, StaircaseUpgradeDef def)
        {
            // Draw background
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(8f);
            
            bool isInstalled = comp.HasActiveUpgrade(def);
            bool isPending = GetPendingUpgrades(SelThing).Contains(def);
            bool isConstructed = comp.HasUpgrade(def);
            UpgradeDisableReason disableReason = UpgradeDisableReason.None;
            if (isConstructed && !isInstalled)
            {
                disableReason = comp.GetUpgradeDisableReason(def);
            }
            
            // Reserve space for buttons at bottom
            float buttonHeight = 35f;
            Rect scrollableRect = new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height - buttonHeight - 4f);
            Rect buttonRect = new Rect(innerRect.x, scrollableRect.yMax + 4f, innerRect.width, buttonHeight);
            
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
                statusColor = new Color(1f, 0.5f, 0f); // Orange
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
            // Add stuff material to status if upgrade is stuffable and installed
            if ((isInstalled || disableReason != UpgradeDisableReason.None) && def.RequiresConstruction && def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
            {
                ActiveUpgrade activeUpgrade = comp.constructedUpgrades.FirstOrDefault(au => au.def == def);
                if (activeUpgrade != null && activeUpgrade.stuff != null)
                {
                    status += $" ({activeUpgrade.stuff.LabelCap})";
                }
            }
            
            GUI.color = statusColor;
            Rect statusRect = new Rect(0f, curY, viewRect.width, 24f);
            Widgets.Label(statusRect, $"Status: {status}");
            GUI.color = Color.white;
            curY += 24f + 6f;
            
            // Description
            float descHeight = Text.CalcHeight(def.description, viewRect.width);
            Rect descRect = new Rect(0f, curY, viewRect.width, descHeight);
            Widgets.Label(descRect, def.description);
            curY += descHeight + 10f;
            
            // Buffs section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Effects:</b>");
            curY += 24f;
            curY = DrawUpgradeEffectsScrollable(0f, curY, viewRect.width, def);
            curY += 10f;
            
            // Costs section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Costs:</b>");
            curY += 24f;
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  Base space: {def.spaceCost}");
            curY += 24f;
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  Space per bed: {def.spaceCostPerBed}");
            curY += 24f;
            
            // Get bed count for cost calculations
            CompMultipleBeds bedsComp = SelThing.TryGetComp<CompMultipleBeds>();
            int bedCount = bedsComp?.bedCount ?? 1;
            
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                ThingDef buildingDef = def.upgradeBuildingDef;
                
                // Check if this uses costStuffCount (stuffable)
                if (buildingDef.costStuffCount > 0)
                {
                    // Get the stuff category label(s)
                    string stuffCategoryLabel = "material";
                    if (def.stuffCategories != null && def.stuffCategories.Count > 0)
                    {
                        if (def.stuffCategories.Count == 1)
                        {
                            stuffCategoryLabel = def.stuffCategories[0].label;
                        }
                        else
                        {
                            stuffCategoryLabel = string.Join(" or ", def.stuffCategories.Select(sc => sc.label));
                        }
                    }
                    
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  Cost: {buildingDef.costStuffCount} {stuffCategoryLabel} per bed");
                    curY += 24f;
                    int totalStuffCost = buildingDef.costStuffCount * bedCount;
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  Total: {totalStuffCost} {stuffCategoryLabel} ({bedCount} beds)");
                    curY += 24f;
                    
                    // If installed, show the material used
                    if (isInstalled)
                    {
                        ActiveUpgrade activeUpgrade = comp.constructedUpgrades.FirstOrDefault(au => au.def == def);
                        if (activeUpgrade != null && activeUpgrade.stuff != null)
                        {
                            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  <color=#00ff00>Material: {activeUpgrade.stuff.LabelCap}</color>");
                            curY += 24f;
                        }
                    }
                }
                
                // Also show regular costList items (if any) - these can exist alongside costStuffCount
                if (buildingDef.costList != null && buildingDef.costList.Count > 0)
                {
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  Additional Materials:");
                    curY += 24f;
                    foreach (var cost in buildingDef.costList)
                    {
                        int available = SelThing.Map.resourceCounter.GetCount(cost.thingDef);
                        string colorTag = available >= cost.count ? "" : "<color=#ff6666>";
                        string colorEnd = available >= cost.count ? "" : "</color>";
                        Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"    {colorTag}{cost.thingDef.label}: {cost.count} (have: {available}){colorEnd}");
                        curY += 24f;
                    }
                }
            }
            curY += 10f;
            
            // Requirements section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Requirements:</b>");
            curY += 24f;
            bool hasRequirements = false;
            
            // Required upgrades
            if (def.requiredUpgrades != null && def.requiredUpgrades.Count > 0)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  Required upgrades:");
                curY += 24f;
                foreach (var requiredUpgrade in def.requiredUpgrades)
                {
                    bool reqInstalled = comp.HasActiveUpgrade(requiredUpgrade);
                    string colorTag = reqInstalled ? "<color=#00ff00>" : "<color=#ff6666>";
                    string colorEnd = "</color>";
                    string reqStatus = reqInstalled ? "✓" : "✗";
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"    {colorTag}{reqStatus} {requiredUpgrade.label}{colorEnd}");
                    curY += 24f;
                }
                hasRequirements = true;
            }
            
            if (def.minConstructionSkill > 0)
            {
                bool hasSkilledPawn = SelThing.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Construction).Level >= def.minConstructionSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  {colorTag}Construction skill: {def.minConstructionSkill}{colorEnd}");
                curY += 24f;
                hasRequirements = true;
            }
            if (def.minArtisticSkill > 0)
            {
                bool hasSkilledPawn = SelThing.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Artistic).Level >= def.minArtisticSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  {colorTag}Artistic skill: {def.minArtisticSkill}{colorEnd}");
                curY += 24f;
                hasRequirements = true;
            }
            if (!hasRequirements)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  None");
                curY += 24f;
            }
            curY += 10f;
            
            // Unlocks section - show what upgrades this one unlocks
            List<StaircaseUpgradeDef> unlocksUpgrades = GetUpgradesUnlockedBy(def);
            if (unlocksUpgrades.Count > 0)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Unlocks:</b>");
                curY += 24f;
                foreach (var unlockedUpgrade in unlocksUpgrades)
                {
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  • {unlockedUpgrade.label}");
                    curY += 24f;
                }
                curY += 10f;
            }
            
            // Store height for next frame
            detailsScrollHeight = curY;
            
            Widgets.EndScrollView();
            
            // Buttons at the bottom - outside scroll view
            if (isInstalled)
            {
                // Show Toggle and Remove buttons for active upgrades that can be toggled
                if (def.CanBeToggled)
                {
                    float buttonWidth = (buttonRect.width - 5f) / 2f;
                    Rect toggleButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                    Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                    
                    // Toggle Off button
                    if (Widgets.ButtonText(toggleButtonRect, "SF_ToggleOff".Translate()))
                    {
                        comp.ToggleUpgrade(def);
                    }
                    TooltipHandler.TipRegion(toggleButtonRect, "SF_ToggleOffTooltip".Translate());
                    
                    // Remove button
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (Widgets.ButtonText(removeButtonRect, "Remove (75% refund)"))
                    {
                        TryRemoveUpgrade(def, comp, SelThing);
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    // Show only Remove button for non-toggleable upgrades
                    if (Widgets.ButtonText(buttonRect, "Remove (75% refund)"))
                    {
                        TryRemoveUpgrade(def, comp, SelThing);
                    }
                }
            }
            else if (disableReason == UpgradeDisableReason.ToggledOff)
            {
                // Upgrade is toggled off - show Toggle On and Remove buttons
                float buttonWidth = (buttonRect.width - 5f) / 2f;
                Rect toggleButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                
                // Toggle On button
                GUI.color = Color.green;
                if (Widgets.ButtonText(toggleButtonRect, "SF_ToggleOn".Translate()))
                {
                    comp.ToggleUpgrade(def);
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(toggleButtonRect, "SF_ToggleOnTooltip".Translate());
                
                // Remove button
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Widgets.ButtonText(removeButtonRect, "Remove (75% refund)"))
                {
                    TryRemoveUpgrade(def, comp, SelThing);
                }
                GUI.color = Color.white;
            }
            else if (disableReason == UpgradeDisableReason.InsufficientCount)
            {
                // Disabled due to not enough constructed - show Add/Remove buttons
                int constructedCount = comp.GetConstructedCount(def);
                CompMultipleBeds bedComp = SelThing.TryGetComp<CompMultipleBeds>();
                int currentBedCount = bedComp?.bedCount ?? 1;
                int needed = currentBedCount - constructedCount;
                
                if (Prefs.DevMode)
                {
                    // Dev mode: show three buttons
                    float buttonWidth = (buttonRect.width - 10f) / 3f;
                    Rect addButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                    Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                    Rect devButtonRect = new Rect(buttonRect.x + buttonWidth * 2 + 10f, buttonRect.y, buttonWidth, buttonRect.height);
                    
                    // "Add More Blueprints" button
                    string addButtonLabel = $"Add {needed} More";
                    if (Widgets.ButtonText(addButtonRect, addButtonLabel))
                    {
                        FillMissingBlueprints(def, comp, SelThing);
                    }
                    TooltipHandler.TipRegion(addButtonRect, 
                        $"This upgrade requires one per bed ({currentBedCount} total). You have {constructedCount} constructed. " +
                        $"Click to place {needed} more blueprint{(needed > 1 ? "s" : "")} to reach the required amount.");
                    
                    // "Remove Constructed" button
                    GUI.color = new Color(1f, 0.5f, 0.5f); // Light red color for remove button
                    if (Widgets.ButtonText(removeButtonRect, $"Remove {constructedCount}"))
                    {
                        TryRemoveConstructedUpgrades(def, comp, SelThing);
                    }
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(removeButtonRect, 
                        $"Remove the {constructedCount} constructed upgrade{(constructedCount > 1 ? "s" : "")} and receive a 75% refund of materials.");
                    
                    // Dev mode instant button
                    GUI.color = new Color(0.8f, 0.5f, 1f); // Purple-ish color for dev button
                    if (Widgets.ButtonText(devButtonRect, "DEV: Instant"))
                    {
                        DevModeInstantUpgrade(def, comp, SelThing);
                    }
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(devButtonRect, "[Dev Mode] Instantly apply this upgrade without materials or construction.");
                }
                else
                {
                    float buttonWidth = (buttonRect.width - 5f) / 2f;
                    Rect addButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                    Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                    
                    // "Add More Blueprints" button
                    string addButtonLabel = $"Add {needed} More";
                    if (Widgets.ButtonText(addButtonRect, addButtonLabel))
                    {
                        FillMissingBlueprints(def, comp, SelThing);
                    }
                    TooltipHandler.TipRegion(addButtonRect, 
                        $"This upgrade requires one per bed ({currentBedCount} total). You have {constructedCount} constructed. " +
                        $"Click to place {needed} more blueprint{(needed > 1 ? "s" : "")} to reach the required amount.");
                    
                    // "Remove Constructed" button
                    GUI.color = new Color(1f, 0.5f, 0.5f); // Light red color for remove button
                    if (Widgets.ButtonText(removeButtonRect, $"Remove {constructedCount}"))
                    {
                        TryRemoveConstructedUpgrades(def, comp, SelThing);
                    }
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(removeButtonRect, 
                        $"Remove the {constructedCount} constructed upgrade{(constructedCount > 1 ? "s" : "")} and receive a 75% refund of materials.");
                }
            }
            else if (disableReason != UpgradeDisableReason.None)
            {
                // Disabled for other reasons (e.g. OutOfFuel, NoPower)
                // If toggleable, show both Toggle Off and Remove buttons
                if (def.CanBeToggled)
                {
                    float buttonWidth = (buttonRect.width - 5f) / 2f;
                    Rect toggleButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                    Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                    
                    // Toggle Off button (even though it's already disabled, let them toggle it off for when power/fuel returns)
                    if (Widgets.ButtonText(toggleButtonRect, "SF_ToggleOff".Translate()))
                    {
                        comp.ToggleUpgrade(def);
                    }
                    TooltipHandler.TipRegion(toggleButtonRect, "SF_ToggleOffTooltip".Translate());
                    
                    // Remove button
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (Widgets.ButtonText(removeButtonRect, "Remove (75% refund)"))
                    {
                        TryRemoveUpgrade(def, comp, SelThing);
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    // Show only Remove button for non-toggleable upgrades
                    if (Widgets.ButtonText(buttonRect, "Remove (75% refund)"))
                    {
                        TryRemoveUpgrade(def, comp, SelThing);
                    }
                }
            }
            else if (!isPending)
            {
                // Check if this is a onePerBed upgrade with some but not enough constructed
                bool isOnePerBed = def.RequiresConstruction && 
                                   def.upgradeBuildingDef?.GetModExtension<StaircaseUpgradeExtension>()?.onePerBed == true;
                int constructedCount = comp.GetConstructedCount(def);
                CompMultipleBeds bedComp = SelThing.TryGetComp<CompMultipleBeds>();
                int currentBedCount = bedComp?.bedCount ?? 1;
                bool needsMoreBlueprints = isOnePerBed && constructedCount > 0 && constructedCount < currentBedCount;
                
                if (needsMoreBlueprints)
                {
                    // Show two buttons side by side: "Add More Blueprints" and "Remove Constructed"
                    int needed = currentBedCount - constructedCount;
                    
                    if (Prefs.DevMode)
                    {
                        // Dev mode: show three buttons
                        float buttonWidth = (buttonRect.width - 10f) / 3f;
                        Rect addButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                        Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                        Rect devButtonRect = new Rect(buttonRect.x + buttonWidth * 2 + 10f, buttonRect.y, buttonWidth, buttonRect.height);
                        
                        // "Add More Blueprints" button
                        string addButtonLabel = $"Add {needed} More";
                        if (Widgets.ButtonText(addButtonRect, addButtonLabel))
                        {
                            FillMissingBlueprints(def, comp, SelThing);
                        }
                        TooltipHandler.TipRegion(addButtonRect, 
                            $"This upgrade requires one per bed ({currentBedCount} total). You have {constructedCount} constructed. " +
                            $"Click to place {needed} more blueprint{(needed > 1 ? "s" : "")} to reach the required amount.");
                        
                        // "Remove Constructed" button
                        GUI.color = new Color(1f, 0.5f, 0.5f); // Light red color for remove button
                        if (Widgets.ButtonText(removeButtonRect, $"Remove {constructedCount}"))
                        {
                            TryRemoveConstructedUpgrades(def, comp, SelThing);
                        }
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(removeButtonRect, 
                            $"Remove the {constructedCount} constructed upgrade{(constructedCount > 1 ? "s" : "")} and receive a 75% refund of materials.");
                        
                        // Dev mode instant button
                        GUI.color = new Color(0.8f, 0.5f, 1f); // Purple-ish color for dev button
                        if (Widgets.ButtonText(devButtonRect, "DEV: Instant"))
                        {
                            DevModeInstantUpgrade(def, comp, SelThing);
                        }
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(devButtonRect, "[Dev Mode] Instantly apply this upgrade without materials or construction.");
                    }
                    else
                    {
                        // Normal mode: show two buttons
                        float buttonWidth = (buttonRect.width - 5f) / 2f;
                        Rect addButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                        Rect removeButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                        
                        // "Add More Blueprints" button
                        string addButtonLabel = $"Add {needed} More";
                        if (Widgets.ButtonText(addButtonRect, addButtonLabel))
                        {
                            FillMissingBlueprints(def, comp, SelThing);
                        }
                        TooltipHandler.TipRegion(addButtonRect, 
                            $"This upgrade requires one per bed ({currentBedCount} total). You have {constructedCount} constructed. " +
                            $"Click to place {needed} more blueprint{(needed > 1 ? "s" : "")} to reach the required amount.");
                        
                        // "Remove Constructed" button
                        GUI.color = new Color(1f, 0.5f, 0.5f); // Light red color for remove button
                        if (Widgets.ButtonText(removeButtonRect, $"Remove {constructedCount}"))
                        {
                            TryRemoveConstructedUpgrades(def, comp, SelThing);
                        }
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(removeButtonRect, 
                            $"Remove the {constructedCount} constructed upgrade{(constructedCount > 1 ? "s" : "")} and receive a 75% refund of materials.");
                    }
                }
                else
                {
                    // Show Build button
                    float availableSpace = comp.GetTotalSpace() - comp.GetUsedSpace();
                    float requiredSpace = def.spaceCost + (def.spaceCostPerBed * (bedComp?.bedCount ?? 1));
                    bool canAffordSpace = availableSpace >= requiredSpace;
                    bool isLocked = IsUpgradeLocked(def, comp);
                    bool canBuild = canAffordSpace && !isLocked;
                    
                    // Dev mode: show two buttons side by side
                    if (Prefs.DevMode)
                    {
                        float buttonWidth = (buttonRect.width - 5f) / 2f;
                        Rect buildButtonRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                        Rect devButtonRect = new Rect(buttonRect.x + buttonWidth + 5f, buttonRect.y, buttonWidth, buttonRect.height);
                        
                        // Regular build button
                        Color oldColor = GUI.color;
                        if (!canBuild)
                        {
                            GUI.color = Color.red;
                        }
                        
                        if (Widgets.ButtonText(buildButtonRect, "Build Upgrade", active: canBuild))
                        {
                            if (canBuild)
                            {
                                TryAddUpgrade(def, comp, SelThing);
                            }
                        }
                        GUI.color = oldColor;
                        
                        if (isLocked)
                        {
                            TooltipHandler.TipRegion(buildButtonRect, "This upgrade is locked. Install the required upgrades first.");
                        }
                        else if (!canAffordSpace)
                        {
                            TooltipHandler.TipRegion(buildButtonRect, $"Not enough space! Need {requiredSpace}, have {availableSpace}");
                        }
                        
                        // Dev mode instant button
                        GUI.color = new Color(0.8f, 0.5f, 1f); // Purple-ish color for dev button
                        if (Widgets.ButtonText(devButtonRect, "DEV: Instant"))
                        {
                            DevModeInstantUpgrade(def, comp, SelThing);
                        }
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(devButtonRect, "[Dev Mode] Instantly apply this upgrade without materials or construction.");
                    }
                    else
                    {
                        // Normal mode: single build button
                        Color oldColor = GUI.color;
                        if (!canBuild)
                        {
                            GUI.color = Color.red;
                        }
                        
                        if (Widgets.ButtonText(buttonRect, "Build Upgrade", active: canBuild))
                        {
                            if (canBuild)
                            {
                                TryAddUpgrade(def, comp, SelThing);
                            }
                        }
                        GUI.color = oldColor;
                        
                        if (isLocked)
                        {
                            TooltipHandler.TipRegion(buttonRect, "This upgrade is locked. Install the required upgrades first.");
                        }
                        else if (!canAffordSpace)
                        {
                            TooltipHandler.TipRegion(buttonRect, $"Not enough space! Need {requiredSpace}, have {availableSpace}");
                        }
                    }
                }
            }
            else
            {
                // Pending - show cancel option
                if (Widgets.ButtonText(buttonRect, "Cancel Construction"))
                {
                    TryCancelUpgrade(def, SelThing);
                }
            }
        }

        private float DrawUpgradeEffectsScrollable(float x, float y, float width, StaircaseUpgradeDef def)
        {
            float curY = y;
            bool hasEffects = false;
            
            if (def.bedCountOffset != 0)
            {
                string sign = def.bedCountOffset > 0 ? "+" : "";
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Bed count: {sign}{def.bedCountOffset}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.bedCountMultiplier != 1f)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Bed count multiplier: x{def.bedCountMultiplier}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.removeSleepDisturbed)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  Removes sleep disturbed");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.impressivenessLevel != 0)
            {
                string sign = def.impressivenessLevel > 0 ? "+" : "";
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Impressiveness: {sign}{def.impressivenessLevel} level(s)");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.thoughtReplacement != null)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  Changes room type thought");
                curY += 24f;
                hasEffects = true;
            }
            
            // Temperature effects
            if (def.heatOffset > 0)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Heating: +{def.heatOffset.ToStringTemperature("F1")}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.maxHeatCap < 100f)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Heats up to: {def.maxHeatCap.ToStringTemperature("F0")}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.coolOffset > 0)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Cooling: -{def.coolOffset.ToStringTemperature("F1")}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.minCoolCap > -273f)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Cools down to: {def.minCoolCap.ToStringTemperature("F0")}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.insulationAdjustment > 0)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Insulation: {def.insulationAdjustment.ToStringTemperature("F1")} towards {def.insulationTarget.ToStringTemperature("F0")}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.fuelPerBed > 0)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Fuel Consumption: {def.fuelPerBed:F1} per bed per day");
                curY += 24f;
                hasEffects = true;
            }
            
            if (!hasEffects)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  No gameplay effects");
                curY += 24f;
            }
            
            return curY;
        }

        private List<StaircaseUpgradeDef> GetApplicableUpgrades(CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> result = new List<StaircaseUpgradeDef>();
            
            foreach (var def in DefDatabase<StaircaseUpgradeDef>.AllDefs)
            {
                // Skip if not applicable to this staircase type
                if (def.applyToStairs != null && !def.applyToStairs.Contains(SelThing.def))
                {
                    continue;
                }
                
                // Skip if research prerequisite is not completed
                if (def.researchPrerequisite != null && !def.researchPrerequisite.IsFinished)
                {
                    continue;
                }
                
                result.Add(def);
            }
            
            return result;
        }

        private Texture2D GetUpgradeIcon(StaircaseUpgradeDef def)
        {
            // Priority 1: Use the texture defined on the upgrade def itself
            if (def.Icon != null)
            {
                return def.Icon;
            }
            
            // Priority 2: Use the LAST item from the cost list (usually the rarest material)
            List<ThingDefCountClass> costs = null;
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                costs = def.upgradeBuildingDef.costList;
            }
            else if (def.costList != null)
            {
                costs = def.costList;
            }
            
            if (costs != null && costs.Count > 0)
            {
                ThingDef lastCostItem = costs[costs.Count - 1].thingDef;
                if (lastCostItem?.uiIcon != null)
                {
                    return lastCostItem.uiIcon;
                }
            }
            
            // Priority 3: Use default power icon (for instant upgrades with no costs)
            return defaultUpgradeIcon;
        }

        private void TryAddUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            float requiredSpace = def.spaceCost + (def.spaceCostPerBed * (staircase.TryGetComp<CompMultipleBeds>()?.bedCount ?? 1));

            if (totalSpace - usedSpace < requiredSpace)
            {
                Messages.Message("SF_NotEnoughSpaceMessage".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Store bed count before the upgrade
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Check if this upgrade is stuffable
            if (def.IsStuffable)
            {
                // Show float menu to select stuff
                ShowStuffSelectionMenu(def, comp, staircase, bedCountBefore);
            }
            else
            {
                // Non-stuffable upgrade - use existing logic
                ApplyUpgrade(def, null, comp, staircase, bedCountBefore);
            }
        }
        
        private void ShowStuffSelectionMenu(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase, int bedCountBefore)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // Get the bed count to calculate material requirements
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCount = bedsComp?.bedCount ?? 1;
            
            // Get base cost from costStuffCount
            int baseCost = 50; // default
            if (def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
            {
                baseCost = def.upgradeBuildingDef.costStuffCount;
            }
            
            // Scale by bed count
            int totalCost = baseCost * bedCount;
            
            // Get all valid stuffs using GenStuff helper
            IEnumerable<ThingDef> allowedStuffs = GenStuff.AllowedStuffsFor(def.upgradeBuildingDef, TechLevel.Undefined);
            
            // Filter to only materials that exist on the map and are not forbidden
            foreach (ThingDef stuff in allowedStuffs)
            {
                // Check if this material exists on the map and is not forbidden
                var availableThings = staircase.Map.listerThings.ThingsOfDef(stuff)
                    .Where(t => !t.IsForbidden(Faction.OfPlayer));
                
                if (!availableThings.Any())
                {
                    continue; // Skip materials not available on map
                }
                
                // Count available materials
                int available = availableThings.Sum(t => t.stackCount);
                bool hasEnough = available >= totalCost;
                
                // Format label: "MaterialName (Available: X)"
                // Color red if insufficient, but still allow placement (vanilla behavior)
                string label;
                if (hasEnough)
                {
                    label = $"{stuff.LabelCap} (Available: {available})";
                }
                else
                {
                    label = $"<color=#ff6666>{stuff.LabelCap} (Available: {available})</color>";
                }
                
                // Always create an enabled option (allow partial materials)
                ThingDef stuffCopy = stuff; // Capture for closure
                options.Add(new FloatMenuOption(label, delegate()
                {
                    ApplyUpgrade(def, stuffCopy, comp, staircase, bedCountBefore);
                }));
            }
            
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("No valid materials available on map", null));
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void ApplyUpgrade(StaircaseUpgradeDef def, ThingDef stuff, CompStaircaseUpgrades comp, Thing staircase, int bedCountBefore)
        {
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                // Place one blueprint per bed count
                if (def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>()?.onePerBed == true)
                {
                    CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
                    int bedCount = bedsComp?.bedCount ?? 1;
                    
                    for (int i = 0; i < bedCount; i++)
                    {
                        PlaceUpgradeBlueprint(def, stuff, staircase);
                    }
                }
                else
                {
                    // Single blueprint
                    PlaceUpgradeBlueprint(def, stuff, staircase);
                }
            }
            else
            {
                // Instant upgrade (legacy behavior)
                comp.AddUpgrade(def, stuff);
                
                // Deduct materials if stuffable
                if (def.IsStuffable && stuff != null)
                {
                    CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
                    int bedCount = bedsComp?.bedCount ?? 1;
                    
                    // Get base cost from costStuffCount
                    int baseCost = 50;
                    if (def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
                    {
                        baseCost = def.upgradeBuildingDef.costStuffCount;
                    }
                    
                    int totalCost = baseCost * bedCount;
                    
                    // Deduct materials from map
                    List<Thing> toRemove = new List<Thing>();
                    int remaining = totalCost;
                    foreach (Thing thing in staircase.Map.listerThings.ThingsOfDef(stuff))
                    {
                        if (remaining <= 0) break;
                        
                        int toTake = Mathf.Min(thing.stackCount, remaining);
                        thing.stackCount -= toTake;
                        remaining -= toTake;
                        
                        if (thing.stackCount <= 0)
                        {
                            toRemove.Add(thing);
                        }
                    }
                    
                    foreach (Thing thing in toRemove)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                }
                
                // Check bed count after upgrade
                CompMultipleBeds bedsComp2 = staircase.TryGetComp<CompMultipleBeds>();
                int bedCountAfter = bedsComp2?.bedCount ?? 0;
                if (bedCountAfter < bedCountBefore)
                {
                    CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade installation");
                }
                
                string materialInfo = stuff != null ? $" ({stuff.LabelCap})" : "";
                Messages.Message("SF_UpgradeInstalled".Translate(def.label + materialInfo, staircase.Label), 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void TryRemoveUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            if (!comp.HasUpgrade(def))
            {
                return;
            }

            // Check if this upgrade is required by any other installed upgrade
            List<StaircaseUpgradeDef> dependentUpgrades = GetInstalledUpgradesThatRequire(def, comp);
            if (dependentUpgrades.Count > 0)
            {
                string dependentNames = string.Join(", ", dependentUpgrades.Select(u => u.label));
                Messages.Message($"Cannot remove {def.label}: It is required by {dependentNames}", staircase, MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Store bed count before removal
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Remove the upgrade with refund
            string refundInfo = comp.RemoveUpgradeWithRefund(def, 0.75f);

            // Check bed count after removal
            int bedCountAfter = bedsComp?.bedCount ?? 0;
            if (bedCountAfter < bedCountBefore)
            {
                CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade removal");
            }

            Messages.Message("SF_UpgradeRemoved".Translate(def.label) + (refundInfo ?? ""), staircase, MessageTypeDefOf.NeutralEvent, false);
            
            // Clear selection since the upgrade is gone
            selectedUpgrade = null;
        }
        
        private void TryRemoveConstructedUpgrades(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            int constructedCount = comp.GetConstructedCount(def);
            if (constructedCount == 0)
            {
                return;
            }

            // Store bed count before removal (won't change since upgrade isn't active yet)
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Remove the constructed upgrades with refund
            string refundInfo = comp.RemoveConstructedUpgradesWithRefund(def, 0.75f);

            // Check bed count after removal (shouldn't change for partial upgrades)
            int bedCountAfter = bedsComp?.bedCount ?? 0;
            if (bedCountAfter < bedCountBefore)
            {
                CheckAndResetBedAssignments(staircase, bedCountAfter, "constructed upgrade removal");
            }

            Messages.Message($"Removed {constructedCount} constructed {def.label} upgrade{(constructedCount > 1 ? "s" : "")}" + (refundInfo ?? ""), 
                staircase, MessageTypeDefOf.NeutralEvent, false);
        }

        private void CheckAndResetBedAssignments(Thing staircase, int newBedCount, string reason)
        {
            Building_Bed bed = staircase as Building_Bed;
            if (bed == null)
            {
                return;
            }

            List<Pawn> owners = bed.OwnersForReading.ToList();
            int currentAssignments = owners.Count;

            if (currentAssignments > newBedCount)
            {
                // Remove excess pawns from assignment (keep the first N pawns)
                for (int i = owners.Count - 1; i >= newBedCount; i--)
                {
                    Pawn pawn = owners[i];
                    pawn.ownership.UnclaimBed();
                }

                // Show message to player
                Messages.Message(
                    "SF_BedAssignmentsReset".Translate(bed.Label, newBedCount),
                    new LookTargets(bed),
                    MessageTypeDefOf.CautionInput,
                    false
                );
            }
        }

        private void TryCancelUpgrade(StaircaseUpgradeDef def, Thing staircase)
        {
            
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            // Find and destroy blueprints/frames for this upgrade
            List<Thing> toDestroy = new List<Thing>();
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Blueprint blueprint = t as Blueprint;
                    if (blueprint != null)
                    {
                        ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                        var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef == def)
                        {
                            toDestroy.Add(t);
                        }
                    }
                }
            }
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Frame frame = t as Frame;
                    if (frame != null)
                    {
                        ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                        var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef == def)
                        {
                            toDestroy.Add(t);
                        }
                    }
                }
            }

            foreach (Thing t in toDestroy)
            {
                t.Destroy(DestroyMode.Cancel);
            }

            if (toDestroy.Count > 0)
            {
                Messages.Message("SF_UpgradeCancelled".Translate(def.label), staircase, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void PlaceUpgradeBlueprint(StaircaseUpgradeDef upgradeDef, ThingDef stuff, Thing staircase)
        {
            ThingDef buildingDef = upgradeDef.upgradeBuildingDef;
            if (buildingDef == null || buildingDef.blueprintDef == null)
            {
                return;
            }

            Map map = staircase.Map;
            IntVec3 position = staircase.Position;
            Rot4 rotation = staircase.Rotation;

            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(
                buildingDef, 
                position, 
                map, 
                rotation, 
                Faction.OfPlayer, 
                stuff
            );

            if (blueprint != null)
            {
                Messages.Message("SF_UpgradePlanned".Translate(upgradeDef.label), 
                    blueprint, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("SF_CannotPlaceBlueprint".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
        
        private void FillMissingBlueprints(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCount = bedsComp?.bedCount ?? 1;
            int constructedCount = comp.GetConstructedCount(def);
            int needed = bedCount - constructedCount;
            
            if (needed <= 0)
            {
                return;
            }
            
            // Get the stuff from the already-constructed upgrade(s)
            ActiveUpgrade activeUpgrade = comp.constructedUpgrades.FirstOrDefault(au => au.def == def);
            ThingDef stuff = activeUpgrade?.stuff;
            
            // Place the needed blueprints
            for (int i = 0; i < needed; i++)
            {
                PlaceUpgradeBlueprint(def, stuff, staircase);
            }
            
            Messages.Message($"Placed {needed} blueprints to complete {def.label}", 
                staircase, MessageTypeDefOf.PositiveEvent, false);
        }

        private List<StaircaseUpgradeDef> GetPendingUpgrades(Thing staircase)
        {
            List<StaircaseUpgradeDef> pending = new List<StaircaseUpgradeDef>();
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Blueprint blueprint = t as Blueprint;
                    if (blueprint != null)
                    {
                        ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                        var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef != null)
                        {
                            pending.Add(ext.upgradeDef);
                        }
                    }
                }
            }

            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Frame frame = t as Frame;
                    if (frame != null)
                    {
                        ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                        var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef != null && !pending.Contains(ext.upgradeDef))
                        {
                            pending.Add(ext.upgradeDef);
                        }
                    }
                }
            }

            return pending;
        }

        private bool IsUpgradeLocked(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
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

        private List<StaircaseUpgradeDef> GetUpgradesUnlockedBy(StaircaseUpgradeDef def)
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

        private List<StaircaseUpgradeDef> GetInstalledUpgradesThatRequire(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
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

        private void DevModeInstantUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            // Dev mode: ignore space requirements and instantly apply upgrade
            
            // Store bed count before the upgrade
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Apply the upgrade directly
            if (!comp.HasUpgrade(def))
            {
                comp.AddUpgrade(def, null);
                
                // Check bed count after upgrade
                int bedCountAfter = bedsComp?.bedCount ?? 0;
                if (bedCountAfter < bedCountBefore)
                {
                    CheckAndResetBedAssignments(staircase, bedCountAfter, "dev mode upgrade");
                }
                
                Messages.Message($"[DEV] {def.label} instantly applied to {staircase.Label}", 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
            // Increase the upgrade count
            else
            {
                comp.IncreaseUpgradeCount(def);
                Messages.Message($"[DEV] Increased {def.label} upgrade count on {staircase.Label}", 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
