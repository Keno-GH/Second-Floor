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
            
            // Empty state - show message
            if (toggleableUpgrades.Count == 0)
            {
                Rect emptyRect = new Rect(rect.x + 10f, labelRect.yMax + 30f, rect.width - 20f, 100f);
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Color.gray;
                Text.Font = GameFont.Small;
                Widgets.Label(emptyRect, "SF_ControlTab_NoToggleables".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, labelRect.yMax + TabLayout.ContentPadding, 
                rect.width, rect.height - labelRect.height - TabLayout.ContentPadding * 2);
            
            float rowHeight = 50f; // Slightly taller for toggle switches
            float viewHeight = toggleableUpgrades.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var def in toggleableUpgrades)
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, rowHeight);
                DrawToggleRow(rowRect, def, comp, staircase);
                curY += rowHeight;
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
            
            Widgets.Label(statusRect, statusText);
            GUI.color = Color.white;
            
            // Consumption info (show total based on installed count)
            int installedCount = comp.GetConstructedCount(def);
            Rect consumptionRect = new Rect(statusRect.xMax + 10f, labelRect.yMax, 150f, 18f);
            string consumptionText = GetConsumptionText(def, installedCount);
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
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Gets total consumption text for the upgrade based on installed count.
        /// </summary>
        private static string GetConsumptionText(StaircaseUpgradeDef def, int installedCount)
        {
            if (installedCount <= 0)
            {
                return "";
            }
            
            if (def.requiresPower && def.basePowerConsumption > 0)
            {
                float totalPower = def.basePowerConsumption * installedCount;
                return $"{totalPower:F0}W";
            }
            
            if (def.fuelPerBed > 0)
            {
                float totalFuel = def.fuelPerBed * installedCount;
                return $"{totalFuel:F1} fuel/day";
            }
            
            return "";
        }
    }
}
