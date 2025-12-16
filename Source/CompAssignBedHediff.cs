using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    class CompAssignBedHediff : ThingComp
    {
        public override void CompTick()
        {
            base.CompTick();
            if (parent.def?.tickerType != TickerType.Normal) return;
            if ((Find.TickManager.TicksGame + parent.thingIDNumber) % 60 != 0) return;
            ApplyHediff();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent.def?.tickerType != TickerType.Rare) return;
            ApplyHediff();
        }

        public override void CompTickLong()
        {
            base.CompTickLong();
            if (parent.def?.tickerType != TickerType.Long) return;
            ApplyHediff();
        }

        private void ApplyHediff()
        {
            var bed = parent as Building_Bed;
            if (bed == null) return;
            var modExt = bed.def.GetModExtension<SecondFloorModExtension>();
            if (modExt?.customHediff == null) return;
            if (bed?.GetComp<CompRefuelable>() != null && !bed.GetComp<CompRefuelable>().HasFuel) return; // Skip if bed is unfueled
            if (bed?.GetComp<CompPowerTrader>() != null && !bed.GetComp<CompPowerTrader>().PowerOn) return; // Skip if bed is unpowered
            foreach (var sleppingOccupant in bed.CurOccupants) if (!sleppingOccupant.health.hediffSet.HasHediff(modExt.customHediff)) sleppingOccupant.health.AddHediff(modExt.customHediff);
        }

    }
}
