using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SecondFloor
{
    class CompatibilityPatches
    {
        public static void ExecuteCompatibilityPatches(Harmony harmony)
        {
        }
    }
    class ShowHairPatches
    {
        public static bool Patch_PawnRenderer_RenderPawnInternal_Postfix_Prefix(Pawn ___pawn)
        {
            if (___pawn?.CurrentBed()?.def?.HasModExtension<SecondFloorModExtension>() == true)
            {
                return false;
            }
            return true;
        }
    }

    class FacialHairStuffPatches
    {
        public static bool HarmonyPatch_PawnRenderer_Prefix_Prefix(PawnRenderer __instance)
        {
            Pawn pawn = __instance.renderTree.pawn;
            if (pawn != null)
            {
                Building_Bed bed = pawn.CurrentBed();
                if (bed != null)
                {
                    ThingDef bedDef = bed.def;
                    if (bedDef != null)
                    {
                        bool hasModExtension = bedDef.HasModExtension<SecondFloorModExtension>();
                        if (hasModExtension)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
