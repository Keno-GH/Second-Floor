using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace SecondFloor
{
    public class StaircaseUpgradeDef : Def
    {
        public float spaceCost;
        public List<ThingDef> applyToStairs;
        public int bedCountOffset;
        public float bedCountMultiplier = 1f;
        public bool removeSleepDisturbed;
        public ThoughtDef thoughtReplacement; // Keep for backwards compatibility
        public int impressivenessLevel = 0; // Adds to the impressiveness level (0-9)

        // Icon texture for this upgrade (optional)
        // Path relative to Textures/ folder (e.g., "Icons/Upgrades/MyUpgrade")
        public string iconPath;
        
        // Required upgrades that must be installed before this one
        public List<StaircaseUpgradeDef> requiredUpgrades;
        
        // Cached texture
        private Texture2D cachedIcon;
        
        public Texture2D Icon
        {
            get
            {
                if (cachedIcon == null && !string.IsNullOrEmpty(iconPath))
                {
                    cachedIcon = ContentFinder<Texture2D>.Get(iconPath, false);
                }
                return cachedIcon;
            }
        }

        // Construction requirements - the actual building ThingDef that colonists construct
        // If null, upgrade is applied instantly (legacy behavior for 1.5 compatibility)
        public ThingDef upgradeBuildingDef;

        // Minimum construction skill required (0 = no requirement)
        public int minConstructionSkill = 0;

        // Minimum artistic skill required (0 = no requirement)
        public int minArtisticSkill = 0;

        // Work amount (if upgradeBuildingDef is null and we want custom work)
        // Note: If upgradeBuildingDef is set, use its WorkToBuild stat instead
        public float workToBuild = 0f;

        // Cost list (if upgradeBuildingDef is null and we want custom costs)
        // Note: If upgradeBuildingDef is set, use its costList instead
        public List<ThingDefCountClass> costList;

        /// <summary>
        /// Returns true if this upgrade requires construction (1.6 behavior)
        /// </summary>
        public bool RequiresConstruction => upgradeBuildingDef != null;

        // Virtual Climate Control fields
        public float heatOffset = 0f; // Amount this upgrade warms the room (e.g., 15)
        public float maxHeatCap = 100f; // The temperature this heater cannot exceed (e.g., 28)
        public float coolOffset = 0f; // Amount this cools the room
        public float minCoolCap = -273f; // The temperature this cooler cannot go below (e.g., 17)
        public float insulationAdjustment = 0f; // Power to normalize temp towards a target
        public float insulationTarget = 21f; // The target temp for insulation (default 21)
        public float fuelPerBed = 0f; // Fuel consumed per bed count per tick
        
        // Stuff system - allows choosing materials for upgrades
        public List<StuffCategoryDef> stuffCategories;
        
        /// <summary>
        /// Returns true if this upgrade can be made from different materials
        /// </summary>
        public bool IsStuffable => !stuffCategories.NullOrEmpty();
    }
}
