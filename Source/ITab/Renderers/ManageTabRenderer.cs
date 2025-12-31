using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace SecondFloor
{
    /// <summary>
    /// Renders the Manage tab content - shows built upgrades with toggle/deconstruct options.
    /// </summary>
    public static class ManageTabRenderer
    {
        /// <summary>
        /// Draws the Manage tab content.
        /// </summary>
        public static void Draw(Rect rect, Thing staircase, CompStaircaseUpgrades comp, 
            ref StaircaseUpgradeDef selectedUpgrade, ref Vector2 scrollPosition)
        {
            List<StaircaseUpgradeDef> builtUpgrades = UpgradeFiltering.GetBuiltUpgrades(comp);
            List<StaircaseUpgradeDef> pendingUpgrades = UpgradeFiltering.GetPendingUpgrades(staircase);
            
            // Draw background
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(TabLayout.ContentPadding);
            
            // Section label
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, TabLayout.StatsLineHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, "SF_ManageTab_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Empty state
            if (builtUpgrades.Count == 0 && pendingUpgrades.Count == 0)
            {
                Rect emptyRect = new Rect(rect.x + 10f, labelRect.yMax + 20f, rect.width - 20f, 80f);
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Color.gray;
                Text.Font = GameFont.Small;
                Widgets.Label(emptyRect, "SF_ManageTab_NoUpgrades".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, labelRect.yMax + TabLayout.ContentPadding, 
                rect.width, rect.height - labelRect.height - TabLayout.ContentPadding * 2);
            
            // Combine built and pending for display
            List<StaircaseUpgradeDef> allUpgrades = new List<StaircaseUpgradeDef>(builtUpgrades);
            foreach (var pending in pendingUpgrades)
            {
                if (!allUpgrades.Contains(pending))
                {
                    allUpgrades.Add(pending);
                }
            }
            
            float viewHeight = allUpgrades.Count * TabLayout.UpgradeRowHeight;
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var def in allUpgrades)
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, TabLayout.UpgradeRowHeight);
                bool isInstalled = comp.HasActiveUpgrade(def);
                bool isPending = pendingUpgrades.Contains(def);
                bool isSelected = selectedUpgrade == def;
                bool isConstructed = comp.HasUpgrade(def);
                
                UpgradeDisableReason disableReason = UpgradeDisableReason.None;
                if (isConstructed && !isInstalled)
                {
                    disableReason = comp.GetUpgradeDisableReason(def);
                }
                
                DrawUpgradeRow(rowRect, def, comp, staircase, isInstalled, isPending, isSelected, 
                    disableReason, ref selectedUpgrade);
                curY += TabLayout.UpgradeRowHeight;
            }
            
            Widgets.EndScrollView();
        }
        
        private static void DrawUpgradeRow(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp, 
            Thing staircase, bool isInstalled, bool isPending, bool isSelected, 
            UpgradeDisableReason disableReason, ref StaircaseUpgradeDef selectedUpgrade)
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
            
            // Icon area
            Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - TabLayout.IconSize) / 2f, 
                TabLayout.IconSize, TabLayout.IconSize);
            
            Texture2D icon = TabAssets.GetUpgradeIcon(def);
            
            bool isDisabled = disableReason != UpgradeDisableReason.None;
            if (isDisabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            
            GUI.DrawTexture(iconRect, icon);
            
            // Draw status overlay
            if (isInstalled)
            {
                Rect checkRect = new Rect(iconRect.xMax - 12f, iconRect.y, 14f, 14f);
                GUI.color = Color.green;
                GUI.DrawTexture(checkRect, TabAssets.InstalledCheckmark);
                GUI.color = Color.white;
            }
            else if (isDisabled)
            {
                Rect warningRect = new Rect(iconRect.xMax - 12f, iconRect.y, 14f, 14f);
                GUI.color = new Color(1f, 0.5f, 0f);
                GUI.DrawTexture(warningRect, TabAssets.WarningIcon);
                GUI.color = Color.white;
            }
            
            // Label with status
            float labelWidth = rect.width - TabLayout.IconSize - 14f;
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, labelWidth, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Color based on status
            if (isDisabled)
            {
                GUI.color = new Color(1f, 0.5f, 0f);
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
            if (isPending && !isInstalled)
            {
                label += " (pending)";
            }
            else if (isDisabled)
            {
                label += " (disabled)";
            }
            
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Handle click
            if (Widgets.ButtonInvisible(rect))
            {
                selectedUpgrade = def;
            }
            
            // Tooltip
            string tooltip = def.description;
            if (isDisabled)
            {
                tooltip += "\n\n<color=#ffaa00>" + GetDisableReasonText(disableReason) + "</color>";
            }
            TooltipHandler.TipRegion(rect, tooltip);
        }
        
        private static string GetDisableReasonText(UpgradeDisableReason reason)
        {
            switch (reason)
            {
                case UpgradeDisableReason.ToggledOff:
                    return "SF_DisableReason_ToggledOff".Translate();
                case UpgradeDisableReason.OutOfFuel:
                    return "SF_DisableReason_OutOfFuel".Translate();
                case UpgradeDisableReason.NoPower:
                    return "SF_DisableReason_NoPower".Translate();
                case UpgradeDisableReason.InsufficientCount:
                    return "SF_DisableReason_InsufficientCount".Translate();
                default:
                    return "SF_DisableReason_Unknown".Translate();
            }
        }
    }
}
