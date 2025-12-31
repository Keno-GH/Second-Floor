using Verse;
using RimWorld;
using UnityEngine;

namespace SecondFloor
{
    /// <summary>
    /// Inspector tab for staircase upgrades with tabbed interface.
    /// Orchestrates the modular renderers for stats, tabs, and details.
    /// </summary>
    public class ITab_Staircase : ITab
    {
        // Tab state - resets when tab is closed
        private UpgradeTabType selectedTab = UpgradeTabType.Manage;
        private StaircaseUpgradeDef selectedUpgrade = null;
        
        // Scroll positions for each tab
        private Vector2 manageScrollPosition = Vector2.zero;
        private Vector2 controlScrollPosition = Vector2.zero;
        private Vector2 constructionScrollPosition = Vector2.zero;

        public ITab_Staircase()
        {
            this.size = TabLayout.WinSize;
            this.labelKey = "TabStaircase";
            this.tutorTag = "Staircase";
        }

        protected override void FillTab()
        {
            TabAssets.EnsureLoaded();
            
            CompStaircaseUpgrades comp = SelThing.TryGetComp<CompStaircaseUpgrades>();
            if (comp == null)
            {
                return;
            }

            Rect mainRect = new Rect(0f, 0f, TabLayout.WinSize.x, TabLayout.WinSize.y).ContractedBy(TabLayout.MainMargin);
            
            // Draw header title
            Rect headerRect = new Rect(mainRect.x, mainRect.y, mainRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "SF_StaircaseDetails".Translate());
            Text.Font = GameFont.Small;
            
            // Calculate stats height for current tab
            float statsHeight = StatsRenderer.CalculateStatsHeight(comp, selectedTab);
            
            // Draw stats panel below header
            float statsY = headerRect.yMax + 5f;
            Rect statsRect = new Rect(mainRect.x, statsY, mainRect.width, statsHeight);
            StatsRenderer.DrawStatsHeader(statsRect, SelThing, comp, selectedTab);
            
            // Draw tab bar below stats
            float tabBarY = statsRect.yMax + TabLayout.SectionSpacing;
            Rect tabBarRect = new Rect(mainRect.x, tabBarY, mainRect.width, TabLayout.TabBarHeight);
            DrawTabBar(tabBarRect);
            
            // Main content area (below tab bar)
            float contentY = tabBarRect.yMax + TabLayout.SectionSpacing;
            Rect contentRect = new Rect(mainRect.x, contentY, mainRect.width, mainRect.yMax - contentY);
            
            // Control tab uses full width (no selection panel)
            if (selectedTab == UpgradeTabType.Control)
            {
                ControlTabRenderer.Draw(contentRect, SelThing, comp, ref controlScrollPosition);
            }
            else
            {
                // Left panel - tab content (upgrade list)
                Rect leftPanel = new Rect(contentRect.x, contentRect.y, TabLayout.LeftPanelWidth, contentRect.height);
                DrawTabContent(leftPanel, comp);
                
                // Right panel - details (only if an upgrade is selected)
                if (selectedUpgrade != null)
                {
                    Rect rightPanel = new Rect(leftPanel.xMax + TabLayout.RightPanelMargin, contentRect.y, 
                        contentRect.width - TabLayout.LeftPanelWidth - TabLayout.RightPanelMargin, contentRect.height);
                    UpgradeDetailsRenderer.Draw(rightPanel, SelThing, comp, selectedUpgrade, ref selectedUpgrade);
                }
            }
        }
        
        /// <summary>
        /// Draws the tab navigation bar with three tabs: Manage, Control, Construction.
        /// </summary>
        private void DrawTabBar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            float tabWidth = (rect.width - TabLayout.TabSpacing * 2) / 3f;
            
            Rect manageTabRect = new Rect(rect.x, rect.y, tabWidth, rect.height);
            Rect controlTabRect = new Rect(manageTabRect.xMax + TabLayout.TabSpacing, rect.y, tabWidth, rect.height);
            Rect constructTabRect = new Rect(controlTabRect.xMax + TabLayout.TabSpacing, rect.y, tabWidth, rect.height);
            
            DrawTab(manageTabRect, "SF_Tab_Manage".Translate(), UpgradeTabType.Manage);
            DrawTab(controlTabRect, "SF_Tab_Control".Translate(), UpgradeTabType.Control);
            DrawTab(constructTabRect, "SF_Tab_Construction".Translate(), UpgradeTabType.Construction);
        }
        
        private void DrawTab(Rect rect, string label, UpgradeTabType tabType)
        {
            bool isSelected = selectedTab == tabType;
            
            // Draw selection highlight
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // Draw label
            Text.Anchor = TextAnchor.MiddleCenter;
            if (isSelected)
            {
                GUI.color = Color.white;
                Widgets.Label(rect, $"<b>{label}</b>");
            }
            else
            {
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                Widgets.Label(rect, label);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Handle click
            if (Widgets.ButtonInvisible(rect))
            {
                selectedTab = tabType;
                selectedUpgrade = null; // Clear selection when switching tabs
            }
            
            // Tooltip
            string tooltip = GetTabTooltip(tabType);
            TooltipHandler.TipRegion(rect, tooltip);
        }
        
        private string GetTabTooltip(UpgradeTabType tabType)
        {
            switch (tabType)
            {
                case UpgradeTabType.Manage:
                    return "SF_Tab_Manage_Tooltip".Translate();
                case UpgradeTabType.Control:
                    return "SF_Tab_Control_Tooltip".Translate();
                case UpgradeTabType.Construction:
                    return "SF_Tab_Construction_Tooltip".Translate();
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Draws the content for the currently selected tab.
        /// </summary>
        private void DrawTabContent(Rect rect, CompStaircaseUpgrades comp)
        {
            switch (selectedTab)
            {
                case UpgradeTabType.Manage:
                    ManageTabRenderer.Draw(rect, SelThing, comp, ref selectedUpgrade, ref manageScrollPosition);
                    break;
                    
                case UpgradeTabType.Control:
                    ControlTabRenderer.Draw(rect, SelThing, comp, ref controlScrollPosition);
                    break;
                    
                case UpgradeTabType.Construction:
                    ConstructionTabRenderer.Draw(rect, SelThing, comp, ref selectedUpgrade, ref constructionScrollPosition);
                    break;
            }
        }
    }
}
