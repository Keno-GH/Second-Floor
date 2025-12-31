using UnityEngine;

namespace SecondFloor
{
    /// <summary>
    /// Layout constants for the staircase upgrade ITab.
    /// </summary>
    public static class TabLayout
    {
        // Main window size
        public static readonly Vector2 WinSize = new Vector2(660f, 600f);
        
        // Left panel (upgrade list) dimensions
        public const float LeftPanelWidth = 220f;
        public const float UpgradeRowHeight = 40f;
        public const float IconSize = 32f;
        
        // Right panel (details) dimensions
        public const float RightPanelMargin = 10f;
        
        // Tab bar dimensions
        public const float TabBarHeight = 30f;
        public const float TabSpacing = 4f;
        
        // Stats header
        public const float StatsLineHeight = 24f;
        public const float SliderHeight = 28f;
        
        // Category headers
        public const float CategoryHeaderHeight = 28f;
        public const float CategoryIndent = 8f;
        
        // Button dimensions
        public const float ButtonHeight = 35f;
        public const float ButtonSpacing = 5f;
        
        // Margins and padding
        public const float MainMargin = 10f;
        public const float SectionSpacing = 10f;
        public const float ContentPadding = 4f;
    }
}
