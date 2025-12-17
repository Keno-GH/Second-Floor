using Verse;
using RimWorld;
using System.Collections.Generic;

namespace SecondFloor
{
    public class CompProperties_StaircaseUpgrades : CompProperties
    {
        public CompProperties_StaircaseUpgrades()
        {
            this.compClass = typeof(CompStaircaseUpgrades);
        }
    }

    public class CompStaircaseUpgrades : ThingComp
    {
        public List<StaircaseUpgradeDef> upgrades = new List<StaircaseUpgradeDef>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
            if (upgrades == null)
            {
                upgrades = new List<StaircaseUpgradeDef>();
            }
        }
        
        public float GetTotalSpace()
        {
            if (parent.GetRoom() != null)
            {
                return parent.GetRoom().CellCount;
            }
            return 0f;
        }

        public float GetUsedSpace()
        {
            float used = 0;
            foreach(var u in upgrades)
            {
                used += u.spaceCost;
            }
            return used;
        }
    }
}
