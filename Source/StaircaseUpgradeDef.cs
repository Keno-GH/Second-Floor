using Verse;
using RimWorld;
using System.Collections.Generic;

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

        // Construction requirements - the actual building ThingDef that colonists construct
        // If null, upgrade is applied instantly (legacy behavior for 1.5 compatibility)
        public ThingDef upgradeBuildingDef;

        // Minimum construction skill required (0 = no requirement)
        public int minConstructionSkill = 0;

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
    }
}
