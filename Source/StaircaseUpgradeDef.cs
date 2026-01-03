using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace SecondFloor
{
    /// <summary>
    /// Defines the type of temperature modifier for smart climate control upgrades.
    /// </summary>
    public enum TempModifierType
    {
        None = 0,      // Not a temperature modifier
        HeaterOnly,    // Can only heat (like a heater)
        CoolerOnly,    // Can only cool (like a cooler) 
        DualMode       // Can both heat and cool (like an AC unit)
    }

    public class StaircaseUpgradeDef : Def
    {
        // =====================================================
        // Category and Display fields
        // =====================================================
        /// <summary>
        /// The category this upgrade belongs to for UI organization.
        /// </summary>
        public UpgradeCategoryDef category;
        
        /// <summary>
        /// Display order within the category (lower = earlier).
        /// </summary>
        public int displayPriority = 0;
        
        /// <summary>
        /// Whether this upgrade appears in the Control tab with a toggle switch.
        /// Set to true for upgrades that can be turned on/off (heaters, coolers, etc.).
        /// </summary>
        public bool isToggleable = false;
        
        /// <summary>
        /// Whether this upgrade can be removed once installed.
        /// Set to false for built-in upgrades that should be permanent (e.g., natural insulation).
        /// </summary>
        public bool canRemove = true;
        
        // =====================================================
        // Core fields
        // =====================================================
        public float spaceCost = 0f; // Space cost this upgrade uses in the staircase
        public List<ThingDef> applyToStairs;
        public int bedCountOffset;
        public float bedCountMultiplier = 1f;
        public bool removeSleepDisturbed;
        public bool removesSleptInDark; // If true, this upgrade provides light and removes the "slept in the dark" thought
        public ThoughtDef thoughtReplacement; // Keep for backwards compatibility
        public int impressivenessLevel = 0; // Adds to the impressiveness level (0-9)
        
        // =====================================================
        // Hospitality Integration fields
        // =====================================================
        /// <summary>
        /// If true, this upgrade increases the virtual bed quality by one tier.
        /// Used by Hospitality to calculate attractiveness for guest beds.
        /// Each upgrade adds 25 points (equivalent to one quality tier).
        /// </summary>
        public bool increasesBedQuality = false;
        
        /// <summary>
        /// If true, this upgrade counts as a connected facility for Hospitality.
        /// Each facility adds 10 points to the bed's attractiveness.
        /// </summary>
        public bool countsAsFacility = false;
        
        // =====================================================
        // Comfort and Sleep Effectiveness fields
        // =====================================================
        /// <summary>
        /// Bonus to the staircase's Comfort stat (applied once per unique upgrade, not per bed).
        /// </summary>
        public float comfortBonus = 0f;
        
        /// <summary>
        /// Bonus to the staircase's BedRestEffectiveness stat (applied once per unique upgrade, not per bed).
        /// </summary>
        public float sleepEffectivenessBonus = 0f;

        // Icon texture for this upgrade (optional)
        // Path relative to Textures/ folder (e.g., "Icons/Upgrades/MyUpgrade")
        public string iconPath;
        
        // Required upgrades that must be installed before this one
        public List<StaircaseUpgradeDef> requiredUpgrades;
        
        // Research prerequisite - upgrade won't appear until this research is completed
        public ResearchProjectDef researchPrerequisite;
        
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

        // =====================================================
        // Virtual Climate Control fields (Dumb temperature modifiers - fuel-based)
        // =====================================================
        public float heatOffset = 0f; // Amount this upgrade warms the room (e.g., 15)
        public float maxHeatCap = 100f; // The temperature this heater cannot exceed (e.g., 28)
        public float coolOffset = 0f; // Amount this cools the room
        public float minCoolCap = -273f; // The temperature this cooler cannot go below (e.g., 17)
        public float insulationAdjustment = 0f; // Power to normalize temp towards a target
        public float insulationTarget = 21f; // The target temp for insulation (default 21)
        public float fuelPerBed = 0f; // Fuel consumed per bed count per tick
        public float spaceCostPerBed = 0f; // Additional space cost per bed count
        
        /// <summary>
        /// If true, this fueled temperature changer will adjust its output to match the target temperature,
        /// rather than always running at full capacity. This allows smart fuel consumption scaling.
        /// Examples: Braziers and Campfires (true), Passive Coolers and Candles (false).
        /// </summary>
        public bool followsDesiredTemp = false;
        
        // =====================================================
        // Power system fields
        // =====================================================
        /// <summary>
        /// Whether this upgrade requires power to operate.
        /// Power-requiring upgrades will be enabled/disabled based on staircase power status.
        /// </summary>
        public bool requiresPower = false;
        
        /// <summary>
        /// Base power consumption in watts (W) for this upgrade.
        /// This is added to the staircase's total power draw.
        /// For smart temperature modifiers, this is the maximum power draw.
        /// </summary>
        public float basePowerConsumption = 0f;
        
        // =====================================================
        // Smart Temperature Modifier fields (power-based with throttling)
        // =====================================================
        /// <summary>
        /// The type of smart temperature modifier.
        /// None = not a smart temp modifier (use dumb heat/cool offsets instead)
        /// HeaterOnly = can only add heat
        /// CoolerOnly = can only remove heat
        /// DualMode = can both heat and cool (like an AC)
        /// </summary>
        public TempModifierType smartTempModifierType = TempModifierType.None;
        
        /// <summary>
        /// The heating efficiency of this smart temperature modifier.
        /// Degrees Celsius added per 100W at full power.
        /// </summary>
        public float smartHeatEfficiency = 0f;
        
        /// <summary>
        /// The cooling efficiency of this smart temperature modifier.
        /// Degrees Celsius removed per 100W at full power.
        /// </summary>
        public float smartCoolEfficiency = 0f;
        
        /// <summary>
        /// Returns true if this is a smart temperature modifier that uses power.
        /// </summary>
        public bool IsSmartTempModifier => smartTempModifierType != TempModifierType.None && requiresPower;
        
        /// <summary>
        /// Returns true if this upgrade is a "dumb" temperature modifier (fuel-based).
        /// These contribute to the base temperature calculation before smart modifiers.
        /// </summary>
        public bool IsDumbTempModifier => (heatOffset > 0f || coolOffset > 0f) && !IsSmartTempModifier;
        
        // Stuff system - allows choosing materials for upgrades
        public List<StuffCategoryDef> stuffCategories;
        
        /// <summary>
        /// Returns true if this upgrade can be made from different materials
        /// </summary>
        public bool IsStuffable => !stuffCategories.NullOrEmpty();
        
        /// <summary>
        /// Returns true if this upgrade can be toggled on/off by the player.
        /// Only upgrades that require fuel or power can be toggled.
        /// </summary>
        public bool CanBeToggled => requiresPower || fuelPerBed > 0f;
        
        // =====================================================
        // Battery Storage fields
        // =====================================================
        /// <summary>
        /// The ThingDef of the invisible battery building to spawn when this upgrade is installed.
        /// This allows using a real vanilla CompPowerBattery for maximum mod compatibility.
        /// If set, the staircase will spawn this building at its position when the upgrade is applied.
        /// The battery building's capacity and efficiency come from its own CompProperties_Battery.
        /// </summary>
        public ThingDef linkedBatteryDef;
        
        /// <summary>
        /// Returns true if this upgrade spawns a battery building.
        /// </summary>
        public bool IsBatteryUpgrade => linkedBatteryDef != null;
        
        // =====================================================
        // Bathroom Upgrade fields (Dubs Bad Hygiene integration)
        // =====================================================
        /// <summary>
        /// The ThingDef of the invisible bathroom building to spawn when this upgrade is installed.
        /// This building should have DBH's CompPipe to connect to the plumbing grid.
        /// </summary>
        public ThingDef linkedBathroomDef;
        
        /// <summary>
        /// Amount of hygiene to restore (0-1 scale). 1.0 = full restore.
        /// </summary>
        public float hygieneRestoreAmount = 0f;
        
        /// <summary>
        /// Maximum hygiene level this upgrade can restore to (0-1 scale).
        /// Use 0.75 for basins (hand washing only), 1.0 for showers/baths.
        /// </summary>
        public float hygieneMaxCap = 1f;
        
        /// <summary>
        /// Amount of bladder need to restore (0-1 scale). 1.0 = full restore.
        /// </summary>
        public float bladderRestoreAmount = 0f;
        
        /// <summary>
        /// Amount of thirst to restore (0-1 scale). 1.0 = full restore.
        /// </summary>
        public float thirstRestoreAmount = 0f;
        
        /// <summary>
        /// Water consumed per bathroom use (in DBH units).
        /// </summary>
        public float waterPerUse = 0f;
        
        /// <summary>
        /// Sewage produced per bathroom use (in DBH units).
        /// </summary>
        public float sewagePerUse = 0f;
        
        /// <summary>
        /// Hot water consumed per use (for showers/baths).
        /// If > 0, the upgrade will try to use hot water first.
        /// </summary>
        public float hotWaterPerUse = 0f;
        
        /// <summary>
        /// Returns true if this is a bathroom upgrade (has any hygiene-related restoration).
        /// </summary>
        public bool IsBathroomUpgrade => linkedBathroomDef != null || 
            hygieneRestoreAmount > 0f || bladderRestoreAmount > 0f || thirstRestoreAmount > 0f;
        
        /// <summary>
        /// Returns true if this upgrade requires hot water for full functionality.
        /// </summary>
        public bool RequiresHotWater => hotWaterPerUse > 0f;
    }
}
