using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace SecondFloor
{
    /// <summary>
    /// Static texture assets for the staircase upgrade ITab.
    /// </summary>
    public static class TabAssets
    {
        private static Texture2D defaultUpgradeIcon;
        private static Texture2D installedCheckmark;
        private static Texture2D warningIcon;
        private static Texture2D expandedIcon;
        private static Texture2D collapsedIcon;
        
        public static Texture2D DefaultUpgradeIcon => defaultUpgradeIcon;
        public static Texture2D InstalledCheckmark => installedCheckmark;
        public static Texture2D WarningIcon => warningIcon;
        public static Texture2D ExpandedIcon => expandedIcon;
        public static Texture2D CollapsedIcon => collapsedIcon;
        
        private static bool loaded = false;
        
        /// <summary>
        /// Loads all static textures. Safe to call multiple times.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            
            defaultUpgradeIcon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false) ?? BaseContent.BadTex;
            installedCheckmark = ContentFinder<Texture2D>.Get("UI/Widgets/CheckOn", false) ?? BaseContent.BadTex;
            warningIcon = ContentFinder<Texture2D>.Get("UI/Icons/Warning", false) ?? BaseContent.BadTex;
            expandedIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Reveal", false) ?? BaseContent.BadTex;
            collapsedIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Add", false) ?? BaseContent.BadTex;
            
            loaded = true;
        }
        
        /// <summary>
        /// Gets the appropriate icon for an upgrade definition.
        /// Priority: 1) Upgrade's iconPath, 2) Last cost item's icon, 3) Default icon.
        /// </summary>
        public static Texture2D GetUpgradeIcon(StaircaseUpgradeDef def)
        {
            EnsureLoaded();
            
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
            
            // Priority 3: Use default power icon
            return defaultUpgradeIcon;
        }
    }
}
