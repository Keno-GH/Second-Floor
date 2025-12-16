using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    /* [HarmonyPatch(typeof(CompProperties_AssignableToPawn), "PostLoadSpecial")]
    public static class CompProperties_AssignableToPawn_PostLoadSpecial_Patch
    {
        public static void Postfix(CompProperties_AssignableToPawn __instance, ThingDef parent)
        {
            if (parent == null) return;
            var multipleBedComp = parent.GetCompProperties<CompProperties_MultipleBeds>();
            if (multipleBedComp == null) return;

            var bedSpots = multipleBedComp.bedCount;
            if (bedSpots < 1) return;
            __instance.maxAssignedPawnsCount = bedSpots;
        }
    } */
}