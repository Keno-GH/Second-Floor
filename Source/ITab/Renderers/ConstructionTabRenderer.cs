using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// Renders the Construction tab content - shows all available upgrades grouped by category.
    /// </summary>
    public static class ConstructionTabRenderer
    {
        // Static state for category expansion - persists during game session
        private static Dictionary<string, bool> categoryExpanded = new Dictionary<string, bool>();
        
        // Height of the expand basement button row
        private const float ExpandButtonHeight = 30f;
        
        /// <summary>
        /// Draws the Construction tab content with collapsible categories.
        /// </summary>
        public static void Draw(Rect rect, Thing staircase, CompStaircaseUpgrades comp, 
            ref StaircaseUpgradeDef selectedUpgrade, ref Vector2 scrollPosition)
        {
            List<StaircaseUpgradeDef> applicableUpgrades = UpgradeFiltering.GetApplicableUpgrades(staircase, comp);
            List<StaircaseUpgradeDef> pendingUpgrades = UpgradeFiltering.GetPendingUpgrades(staircase);
            
            // Group by category
            var grouped = UpgradeFiltering.GroupByCategory(applicableUpgrades);
            
            // Draw background
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(TabLayout.ContentPadding);
            
            // Section label
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, TabLayout.StatsLineHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, "SF_ConstructionTab_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            float scrollStartY = labelRect.yMax + TabLayout.ContentPadding;
            
            // Check if this is a basement and draw expand button if so
            var expansionComp = staircase.TryGetComp<CompBasementExpansion>();
            if (expansionComp != null)
            {
                Rect buttonRect = new Rect(rect.x, scrollStartY, rect.width, ExpandButtonHeight);
                DrawExpandBasementButton(buttonRect, expansionComp);
                scrollStartY = buttonRect.yMax + TabLayout.ContentPadding;
            }
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, scrollStartY, 
                rect.width, rect.yMax - scrollStartY - TabLayout.ContentPadding);
            
            // Calculate total height (including uncategorized upgrades)
            float viewHeight = CalculateViewHeight(grouped, applicableUpgrades, comp);
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var kvp in grouped)
            {
                UpgradeCategoryDef category = kvp.Key;
                List<StaircaseUpgradeDef> upgrades = kvp.Value;
                
                // Initialize expansion state from default if not set
                if (!categoryExpanded.ContainsKey(category.defName))
                {
                    categoryExpanded[category.defName] = category.defaultExpanded;
                }
                
                // Draw category header
                Rect headerRect = new Rect(0f, curY, viewRect.width, TabLayout.CategoryHeaderHeight);
                bool isExpanded = categoryExpanded[category.defName];
                
                if (DrawCategoryHeader(headerRect, category, upgrades.Count, isExpanded))
                {
                    categoryExpanded[category.defName] = !isExpanded;
                    isExpanded = !isExpanded;
                }
                curY += TabLayout.CategoryHeaderHeight;
                
                // Draw upgrades if expanded
                if (isExpanded)
                {
                    foreach (var def in upgrades)
                    {
                        Rect rowRect = new Rect(TabLayout.CategoryIndent, curY, 
                            viewRect.width - TabLayout.CategoryIndent, TabLayout.UpgradeRowHeight);
                        
                        bool isInstalled = comp.HasActiveUpgrade(def);
                        bool isPending = pendingUpgrades.Contains(def);
                        bool isSelected = selectedUpgrade == def;
                        bool isLocked = UpgradeFiltering.IsUpgradeLocked(def, comp);
                        bool isConstructed = comp.HasUpgrade(def);
                        
                        UpgradeDisableReason disableReason = UpgradeDisableReason.None;
                        if (isConstructed && !isInstalled)
                        {
                            disableReason = comp.GetUpgradeDisableReason(def);
                        }
                        
                        DrawUpgradeRow(rowRect, def, comp, isInstalled, isPending, isSelected, 
                            isLocked, disableReason, ref selectedUpgrade);
                        curY += TabLayout.UpgradeRowHeight;
                    }
                }
            }
            
            // Handle uncategorized upgrades
            var uncategorized = applicableUpgrades.Where(u => u.category == null).ToList();
            if (uncategorized.Count > 0)
            {
                // Draw "Other" category
                Rect otherHeaderRect = new Rect(0f, curY, viewRect.width, TabLayout.CategoryHeaderHeight);
                
                if (!categoryExpanded.ContainsKey("__uncategorized"))
                {
                    categoryExpanded["__uncategorized"] = true;
                }
                
                bool otherExpanded = categoryExpanded["__uncategorized"];
                if (DrawUncategorizedHeader(otherHeaderRect, uncategorized.Count, otherExpanded))
                {
                    categoryExpanded["__uncategorized"] = !otherExpanded;
                    otherExpanded = !otherExpanded;
                }
                curY += TabLayout.CategoryHeaderHeight;
                
                if (otherExpanded)
                {
                    foreach (var def in uncategorized)
                    {
                        Rect rowRect = new Rect(TabLayout.CategoryIndent, curY, 
                            viewRect.width - TabLayout.CategoryIndent, TabLayout.UpgradeRowHeight);
                        
                        bool isInstalled = comp.HasActiveUpgrade(def);
                        bool isPending = pendingUpgrades.Contains(def);
                        bool isSelected = selectedUpgrade == def;
                        bool isLocked = UpgradeFiltering.IsUpgradeLocked(def, comp);
                        bool isConstructed = comp.HasUpgrade(def);
                        
                        UpgradeDisableReason disableReason = UpgradeDisableReason.None;
                        if (isConstructed && !isInstalled)
                        {
                            disableReason = comp.GetUpgradeDisableReason(def);
                        }
                        
                        DrawUpgradeRow(rowRect, def, comp, isInstalled, isPending, isSelected, 
                            isLocked, disableReason, ref selectedUpgrade);
                        curY += TabLayout.UpgradeRowHeight;
                    }
                }
            }
            
            Widgets.EndScrollView();
        }
        
        private static float CalculateViewHeight(Dictionary<UpgradeCategoryDef, List<StaircaseUpgradeDef>> grouped, 
            List<StaircaseUpgradeDef> applicableUpgrades, CompStaircaseUpgrades comp)
        {
            float height = 0f;
            
            foreach (var kvp in grouped)
            {
                height += TabLayout.CategoryHeaderHeight;
                
                if (!categoryExpanded.ContainsKey(kvp.Key.defName))
                {
                    categoryExpanded[kvp.Key.defName] = kvp.Key.defaultExpanded;
                }
                
                if (categoryExpanded[kvp.Key.defName])
                {
                    height += kvp.Value.Count * TabLayout.UpgradeRowHeight;
                }
            }
            
            // Include uncategorized upgrades in height calculation
            var uncategorized = applicableUpgrades.Where(u => u.category == null).ToList();
            if (uncategorized.Count > 0)
            {
                height += TabLayout.CategoryHeaderHeight; // "Other" header
                
                if (!categoryExpanded.ContainsKey("__uncategorized"))
                {
                    categoryExpanded["__uncategorized"] = true;
                }
                
                if (categoryExpanded["__uncategorized"])
                {
                    height += uncategorized.Count * TabLayout.UpgradeRowHeight;
                }
            }
            
            return height;
        }
        
        private static bool DrawCategoryHeader(Rect rect, UpgradeCategoryDef category, int upgradeCount, bool isExpanded)
        {
            // Background
            Widgets.DrawLightHighlight(rect);
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // Expand/collapse arrow
            Rect arrowRect = new Rect(rect.x + 4f, rect.y + (rect.height - 16f) / 2f, 16f, 16f);
            Texture2D arrowTex = isExpanded ? TabAssets.ExpandedIcon : TabAssets.CollapsedIcon;
            GUI.DrawTexture(arrowRect, arrowTex);
            
            // Category label
            Rect labelRect = new Rect(arrowRect.xMax + 4f, rect.y, rect.width - 80f, rect.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, $"<b>{category.label}</b>");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Count badge
            Rect countRect = new Rect(rect.xMax - 40f, rect.y, 36f, rect.height);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.gray;
            Widgets.Label(countRect, $"({upgradeCount})");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Tooltip
            if (!string.IsNullOrEmpty(category.description))
            {
                TooltipHandler.TipRegion(rect, category.description);
            }
            
            return Widgets.ButtonInvisible(rect);
        }
        
        private static bool DrawUncategorizedHeader(Rect rect, int upgradeCount, bool isExpanded)
        {
            Widgets.DrawLightHighlight(rect);
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            Rect arrowRect = new Rect(rect.x + 4f, rect.y + (rect.height - 16f) / 2f, 16f, 16f);
            Texture2D arrowTex = isExpanded ? TabAssets.ExpandedIcon : TabAssets.CollapsedIcon;
            GUI.DrawTexture(arrowRect, arrowTex);
            
            Rect labelRect = new Rect(arrowRect.xMax + 4f, rect.y, rect.width - 80f, rect.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(labelRect, $"<b>{"SF_Category_Other".Translate()}</b>");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            Rect countRect = new Rect(rect.xMax - 40f, rect.y, 36f, rect.height);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.gray;
            Widgets.Label(countRect, $"({upgradeCount})");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return Widgets.ButtonInvisible(rect);
        }
        
        private static void DrawUpgradeRow(Rect rect, StaircaseUpgradeDef def, CompStaircaseUpgrades comp,
            bool isInstalled, bool isPending, bool isSelected, bool isLocked, 
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
            bool hasSkillRequirement = def.minConstructionSkill > 0 || def.minArtisticSkill > 0;
            bool skillsUnavailable = hasSkillRequirement && !HasRequiredSkills(def, comp.parent);
            
            if (isLocked || isDisabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            
            GUI.DrawTexture(iconRect, icon);
            
            // Status overlay
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
            
            // Label
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, rect.width - TabLayout.IconSize - 14f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Color based on status
            if (isLocked)
            {
                GUI.color = Color.gray;
            }
            else if (isDisabled)
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
            else if (skillsUnavailable)
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
            else if (isLocked)
            {
                label += " (locked)";
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
            if (isLocked)
            {
                tooltip += "\n\n<color=#ff6666>" + "SF_Tooltip_RequiresUpgrades".Translate() + "</color>";
            }
            else if (isDisabled)
            {
                tooltip += "\n\n<color=#ffaa00>" + GetDisableReasonText(disableReason) + "</color>";
            }
            else if (skillsUnavailable)
            {
                tooltip += "\n\n<color=#ffff00>" + "SF_Tooltip_SkillsUnavailable".Translate() + "</color>";
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
        
        /// <summary>
        /// Checks if the colony has pawns with the required construction or artistic skills.
        /// </summary>
        private static bool HasRequiredSkills(StaircaseUpgradeDef def, Thing staircase)
        {
            if (staircase?.Map == null)
            {
                return true; // Can't check, assume skills are available
            }
            
            bool hasConstructionSkill = true;
            bool hasArtisticSkill = true;
            
            if (def.minConstructionSkill > 0)
            {
                hasConstructionSkill = staircase.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills != null && p.skills.GetSkill(SkillDefOf.Construction).Level >= def.minConstructionSkill);
            }
            
            if (def.minArtisticSkill > 0)
            {
                hasArtisticSkill = staircase.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills != null && p.skills.GetSkill(SkillDefOf.Artistic).Level >= def.minArtisticSkill);
            }
            
            return hasConstructionSkill && hasArtisticSkill;
        }
        
        /// <summary>
        /// Draws the "Expand Basement" button for basement staircases.
        /// </summary>
        private static void DrawExpandBasementButton(Rect rect, CompBasementExpansion expansionComp)
        {
            // Draw background
            Widgets.DrawLightHighlight(rect);
            
            bool isMaxed = expansionComp.IsMaxExpansion;
            bool inProgress = expansionComp.IsExcavationInProgress;
            bool canExpand = !isMaxed && !inProgress;
            
            // Button text
            string buttonLabel;
            string tooltip;
            
            if (isMaxed)
            {
                buttonLabel = "SF_ExpandBasement_Maxed".Translate();
                tooltip = "SF_ExpandBasement_Maxed_Tooltip".Translate(expansionComp.MaxSpace);
            }
            else if (inProgress)
            {
                int mined = 5 - expansionComp.MinedCountInBatch;
                buttonLabel = "SF_ExpandBasement_InProgress".Translate(expansionComp.MinedCountInBatch, 5);
                tooltip = "SF_ExpandBasement_InProgress_Tooltip".Translate();
            }
            else
            {
                buttonLabel = "SF_ExpandBasement".Translate();
                tooltip = "SF_ExpandBasement_Tooltip".Translate(expansionComp.TotalSpace, expansionComp.MaxSpace);
            }
            
            // Draw the button
            if (!canExpand)
            {
                GUI.color = Color.gray;
            }
            
            if (Widgets.ButtonText(rect, buttonLabel, active: canExpand))
            {
                if (canExpand)
                {
                    expansionComp.SpawnExpansionRocks();
                }
            }
            
            GUI.color = Color.white;
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}
