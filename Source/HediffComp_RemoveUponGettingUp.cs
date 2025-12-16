using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    class HediffComp_RemoveUponGettingUp : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (parent?.pawn?.CurrentBed()?.def?.HasModExtension<SecondFloorModExtension>() != true) parent.pawn.health.RemoveHediff(this.parent);
        }

    }
}
