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
        public bool removeSleepDisturbed;
    }
}
