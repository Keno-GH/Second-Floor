using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace SecondFloor
{
    /// <summary>
    /// Renders the Control tab content - shows toggleable upgrades with on/off switches.
    /// </summary>
    public static class ControlTabRenderer
    {
        /// <summary>
        /// Draws the Control tab content.
        /// </summary>
        public static void Draw(Rect rect, Thing staircase, CompStaircaseUpgrades comp, ref Vector2 scrollPosition)
        {
            List<StaircaseUpgradeDef> toggleableUpgrades = UpgradeFiltering.GetToggleableUpgrades(comp);
            
            // Draw background
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(TabLayout.ContentPadding);
            
            // Section label
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, TabLayout.StatsLineHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, "SF_ControlTab_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            float curY = labelRect.yMax + TabLayout.ContentPadding;
            
            // Priority toggle (only show if both controllable fueled and smart temp changers are present)
            float priorityToggleHeight = 0f;
            if (comp.HasControllableFueledAndSmartTempChangers)
            {
                priorityToggleHeight = DrawPriorityToggle(new Rect(rect.x, curY, rect.width, 28f), comp);
                curY += priorityToggleHeight + TabLayout.ContentPadding;
            }
            
            // Empty state - show message
            if (toggleableUpgrades.Count == 0)
            {
                Rect emptyRect = new Rect(rect.x + 10f, curY + 20f, rect.width - 20f, 100f);
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Color.gray;
                Text.Font = GameFont.Small;
                Widgets.Label(emptyRect, "SF_ControlTab_NoToggleables".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, curY, 
                rect.width, rect.height - (curY - rect.y) - TabLayout.ContentPadding);
            
            float rowHeight = 50f; // Slightly taller for toggle switches
            float viewHeight = toggleableUpgrades.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float scrollY = 0f;
            foreach (var def in toggleableUpgrades)
            {
                Rect rowRect = new Rect(0f, scrollY, viewRect.width, rowHeight);
                DrawToggleRow(rowRect, def, comp, staircase);
                scrollY += rowHeight;
            }
            
            Widgets.EndScrollView();
        }
        
        private static void DrawToggleRow(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            // Alternating background
            int index = DefDatabase<StaircaseUpgradeDef>.AllDefsListForReading.IndexOf(def);
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // Get state
            bool isActive = comp.HasActiveUpgrade(def);
            UpgradeDisableReason disableReason = comp.GetUpgradeDisableReason(def);
            bool isToggledOff = disableReason == UpgradeDisableReason.ToggledOff;
            bool isOnStandby = disableReason == UpgradeDisableReason.ReachedTemperature;
            bool hasResourceIssue = disableReason == UpgradeDisableReason.NoPower || 
                                    disableReason == UpgradeDisableReason.OutOfFuel;
            
            // Icon
            Rect iconRect = new Rect(rect.x + 8f, rect.y + (rect.height - TabLayout.IconSize) / 2f, 
                TabLayout.IconSize, TabLayout.IconSize);
            
            Texture2D icon = TabAssets.GetUpgradeIcon(def);
            
            if (!isActive)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            GUI.DrawTexture(iconRect, icon);
            GUI.color = Color.white;
            
            // Label and status
            Rect labelRect = new Rect(iconRect.xMax + 10f, rect.y + 4f, rect.width - 150f, 20f);
            Text.Font = GameFont.Small;
            
            if (isActive)
            {
                GUI.color = Color.green;
            }
            else if (isOnStandby)
            {
                GUI.color = new Color(0.4f, 0.8f, 1f); // Cyan for standby
            }
            else if (isToggledOff)
            {
                GUI.color = Color.gray;
            }
            else if (hasResourceIssue)
            {
                GUI.color = new Color(1f, 0.5f, 0f);
            }
            
            Widgets.Label(labelRect, def.label);
            GUI.color = Color.white;
            
            // Status text + consumption info
            Rect statusRect = new Rect(iconRect.xMax + 10f, labelRect.yMax, 180f, 18f);
            Text.Font = GameFont.Tiny;
            
            string statusText = "";
            if (isActive)
            {
                statusText = "SF_Status_Active".Translate();
                GUI.color = Color.green;
            }
            else if (isToggledOff)
            {
                statusText = "SF_Status_ToggledOff".Translate();
                GUI.color = Color.gray;
            }
            else if (disableReason == UpgradeDisableReason.NoPower)
            {
                statusText = "SF_Status_NoPower".Translate();
                GUI.color = Color.red;
            }
            else if (disableReason == UpgradeDisableReason.OutOfFuel)
            {
                statusText = "SF_Status_OutOfFuel".Translate();
                GUI.color = Color.red;
            }
            else if (disableReason == UpgradeDisableReason.InsufficientCount)
            {
                statusText = "SF_Status_Incomplete".Translate();
                GUI.color = new Color(1f, 0.5f, 0f);
            }
            else if (disableReason == UpgradeDisableReason.ReachedTemperature)
            {
                statusText = "SF_Status_ReachedTemperature".Translate();
                GUI.color = new Color(0.4f, 0.8f, 1f); // Cyan for standby
            }
            
            Widgets.Label(statusRect, statusText);
            GUI.color = Color.white;
            
            // Consumption info (show total based on installed count)
            int installedCount = comp.GetConstructedCount(def);
            Rect consumptionRect = new Rect(statusRect.xMax + 10f, labelRect.yMax, 150f, 18f);
            string consumptionText = GetConsumptionText(def, installedCount, comp);
            if (!string.IsNullOrEmpty(consumptionText))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(consumptionRect, consumptionText);
                GUI.color = Color.white;
            }
            
            Text.Font = GameFont.Small;
            
            // Toggle button on the right
            float toggleWidth = 80f;
            Rect toggleRect = new Rect(rect.xMax - toggleWidth - 10f, rect.y + (rect.height - 30f) / 2f, 
                toggleWidth, 30f);
            
            string buttonLabel = isToggledOff ? "SF_Toggle_TurnOn".Translate() : "SF_Toggle_TurnOff".Translate();
            Color buttonColor = isToggledOff ? Color.green : new Color(1f, 0.6f, 0.2f);
            
            GUI.color = buttonColor;
            if (Widgets.ButtonText(toggleRect, buttonLabel))
            {
                comp.ToggleUpgrade(def);
            }
            GUI.color = Color.white;
            
            // Tooltip
            string tooltip = def.description;
            if (!isActive && disableReason != UpgradeDisableReason.None)
            {
                tooltip += "\n\n<color=#ffaa00>" + GetStatusTooltip(disableReason) + "</color>";
            }
            TooltipHandler.TipRegion(rect, tooltip);
        }
        
        private static string GetStatusTooltip(UpgradeDisableReason reason)
        {
            switch (reason)
            {
                case UpgradeDisableReason.ToggledOff:
                    return "SF_Tooltip_ToggledOff".Translate();
                case UpgradeDisableReason.NoPower:
                    return "SF_Tooltip_NoPower".Translate();
                case UpgradeDisableReason.OutOfFuel:
                    return "SF_Tooltip_OutOfFuel".Translate();
                case UpgradeDisableReason.InsufficientCount:
                    return "SF_Tooltip_InsufficientCount".Translate();
                case UpgradeDisableReason.ReachedTemperature:
                    return "SF_Tooltip_ReachedTemperature".Translate();
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Draws the priority toggle for fueled vs electric temp changers.
        /// </summary>
        private static float DrawPriorityToggle(Rect rect, CompStaircaseUpgrades comp)
        {
            Text.Font = GameFont.Small;
            
            string label = comp.preferFueledFirst 
                ? "SF_PreferFueledFirst".Translate() 
                : "SF_PreferElectricFirst".Translate();
            string tooltip = comp.preferFueledFirst 
                ? "SF_PreferFueledFirst_Tooltip".Translate() 
                : "SF_PreferElectricFirst_Tooltip".Translate();
            
            // Button to toggle preference
            GUI.color = comp.preferFueledFirst ? new Color(1f, 0.7f, 0.3f) : new Color(0.5f, 0.8f, 1f);
            if (Widgets.ButtonText(rect, label))
            {
                comp.preferFueledFirst = !comp.preferFueledFirst;
            }
            GUI.color = Color.white;
            
            TooltipHandler.TipRegion(rect, tooltip);
            
            return rect.height;
        }
        
        /// <summary>
        /// Gets total consumption text for the upgrade based on installed count.
        /// For controllable fueled temp changers, shows actual consumption with throttle percentage.
        /// For smart temp changers, shows actual power consumption with throttle percentage.
        /// </summary>
        private static string GetConsumptionText(StaircaseUpgradeDef def, int installedCount, CompStaircaseUpgrades comp)
        {
            if (installedCount <= 0)
            {
                return "";
            }
            
            if (def.requiresPower && def.basePowerConsumption > 0)
            {
                float totalPower = def.basePowerConsumption * installedCount;
                
                // For smart temp changers, show actual consumption with throttle
                if (def.IsSmartTempModifier)
                {
                    float utilizationRatio = comp.GetSmartUtilizationRatio(def);
                    float actualPower = totalPower * utilizationRatio;
                    int throttlePercent = Mathf.RoundToInt(utilizationRatio * 100f);
                    return $"{actualPower:F0}W ({throttlePercent}%)";
                }
                
                return $"{totalPower:F0}W";
            }
            
            if (def.fuelPerBed > 0)
            {
                float maxFuel = def.fuelPerBed * installedCount;
                
                // For controllable fueled temp changers, show actual consumption with throttle
                if (def.followsDesiredTemp && def.heatOffset > 0f)
                {
                    float utilizationRatio = comp.GetFueledUtilizationRatio();
                    float actualFuel = maxFuel * utilizationRatio;
                    int throttlePercent = Mathf.RoundToInt(utilizationRatio * 100f);
                    return $"{actualFuel:F1} fuel/day ({throttlePercent}%)";
                }
                
                return $"{maxFuel:F1} fuel/day";
            }
            
            return "";
        }
    }
}
