using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    public class CompProperties_GiveThoughtStairs : CompProperties
    {
        public CompProperties_GiveThoughtStairs()
        {

            this.compClass = typeof(CompGiveThoughtStairs);
        }

        public ThoughtDef thoughtDef;
        public int radius = 0;
        public bool enableInInventory = false;
    }
}
