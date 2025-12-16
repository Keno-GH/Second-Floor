using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    public class CompGiveThoughtStairs : ThingComp
    {
        public CompProperties_GiveThoughtStairs Props => (CompProperties_GiveThoughtStairs)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.def?.tickerType != TickerType.Normal) return;
            if ((Find.TickManager.TicksGame + parent.thingIDNumber) % 60 != 0) return;
            ApplyThought();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent.def?.tickerType != TickerType.Rare) return;
            ApplyThought();
        }

        public override void CompTickLong()
        {
            base.CompTickLong();
            if (parent.def?.tickerType != TickerType.Long) return;
            ApplyThought();
        }

        protected void ApplyThought()
        {
            if (parent is Building_Bed bed)
            {
                if (bed?.CurOccupants == null) return;
                if (bed?.GetComp<CompRefuelable>() != null && !bed.GetComp<CompRefuelable>().HasFuel) return; // Skip if bed is unfueled
                if (bed?.GetComp<CompPowerTrader>() != null && !bed.GetComp<CompPowerTrader>().PowerOn) return; // Skip if bed is unpowered
                foreach (var sleppingOccupant in bed.CurOccupants) sleppingOccupant.needs.mood.thoughts.memories.TryGainMemory(Props.thoughtDef);
            }
        }

    }
}
