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
        public int impressivenessLevel = 0; // New: Adds to the impressiveness level (0-9)
    }
}
