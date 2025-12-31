using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Defines a category for organizing staircase upgrades in the ITab.
    /// Categories are displayed as collapsible sections in the Construction tab.
    /// </summary>
    public class UpgradeCategoryDef : Def
    {
        /// <summary>
        /// Display order in the category list (lower = earlier).
        /// </summary>
        public int displayOrder = 0;
        
        /// <summary>
        /// Whether this category should be expanded by default.
        /// </summary>
        public bool defaultExpanded = true;
    }
}
