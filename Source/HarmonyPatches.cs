using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.AI;
using System.Reflection.Emit;

namespace SecondFloor
{
    [StaticConstructorOnStartup]
    public class MainHarmonyInstance : Mod
    {
        public MainHarmonyInstance(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("avericat.SecondFloors");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // CompatibilityPatches.ExecuteCompatibilityPatches(harmony);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer))]
    [HarmonyPatch("RenderPawnAt")]
    [HarmonyPatch(new Type[] { 
        typeof(Vector3),
        typeof(Rot4?),
        typeof(bool)
    })]
    public class PawnRenderer_RenderPawnAt
    {
        public static bool Prefix(Vector3 drawLoc, Rot4? rotOverride, bool neverAimWeapon, Pawn ___pawn)
        {
            // Skip if pawn is invalid or not humanlike
            if (___pawn?.Map == null || ___pawn?.RaceProps?.Humanlike != true) 
                return true;
                
            // Hide pawn if they're in a second floor bed
            return !(___pawn?.CurrentBed()?.def?.HasModExtension<SecondFloorModExtension>() == true);
        }
    }

    // Remove the SoakingWet thought if the pawn is in a bed with RemoveSoakingWet set to true
    // Weather thoughts are not memories, so they need to be removed by using weatherManager.CurWeatherLerped
    /* [HarmonyPatch(typeof(Pawn_MindState), "MindStateTick")]
    public class Pawn_MindState_MindStateTick
    {
        public static void Postfix(Pawn_MindState __instance)
        {
            if (Find.TickManager.TicksGame % 123 == 0 && __instance.pawn.Spawned && __instance.pawn.RaceProps.IsFlesh && __instance.pawn.needs.mood != null)
            {
                var currBed = __instance.pawn.CurrentBed();
                if (currBed == null) return;
                var modExt = currBed.def.GetModExtension<SecondFloorModExtension>();
                if (modExt == null || !modExt.RemoveSoakingWet) return;

                WeatherDef curWeatherLerped = __instance.pawn.Map.weatherManager.CurWeatherLerped;
                if (curWeatherLerped.weatherThought != null && curWeatherLerped.weatherThought == ThoughtDef.Named("SoakingWet") && !__instance.pawn.Position.Roofed(__instance.pawn.Map))
                {
                    __instance.pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(curWeatherLerped.weatherThought);
                }
            }
        }
    } */ // Unecessary since SoakingWet is only applied when outside and our second floors will always require a roof

    // Remove memory thoughts when pawns sleep in a second floor bed with the appropriate settings
    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts", new Type[] { typeof(Pawn), typeof(Building_Bed) })]
    public class Toils_LayDown_ApplyBedThoughts
    {
        public static void Postfix(Pawn actor)
        {
            Building_Bed building_Bed = actor.CurrentBed();
            if (building_Bed == null) return;
            var modExt = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (modExt == null) return;

            var effect = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (effect == null) return;
            if (effect.RemoveSleptOutside) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);
            if (effect.RemoveSleptInCold) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInCold);
            if (effect.RemoveSleptInHeat) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInHeat);
            if (effect.RemoveSleptInBarracks) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
            if (effect.RemoveSleepDisturbed) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleepDisturbed);
            if (effect.RemoveSleptInBedroom) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBedroom);
            //if (effect.RemoveSunlightSensitivity_Mild) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SunlightSensitivity_Mild);

            var upgradesComp = building_Bed.GetComp<CompStaircaseUpgrades>();
            if (upgradesComp != null)
            {
                foreach (var upgrade in upgradesComp.upgrades)
                {
                    if (upgrade.removeSleepDisturbed)
                    {
                        actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleepDisturbed);
                    }
                }
            }
        }
    }


    // Remove SharedBed Tought by postfixing the ThoughtWorker
    // SharedBed is a ThoughtWorker that checks if a pawn is sharing a bed with another pawn
    // Since its not a memory, we can't remove it with RemoveMemoriesOfDef and we have
    // to use a postfix to change the result to false if the bed has RemoveSharedBed set to true
    [HarmonyPatch(typeof(ThoughtWorker_SharedBed), "CurrentStateInternal")]
    public static class Patch_CurrentStateInternal
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            Building_Bed building_Bed = p.ownership.OwnedBed;
            if (building_Bed == null) return;
            var modExt = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (modExt == null) return;

            var effect = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (effect == null) return;
            if (effect.RemoveSharedBed) __result = false;
        }
    }

    // Remove Greedy Thought by postfixing the ThoughtWorker
    [HarmonyPatch(typeof(ThoughtWorker_Greedy), "CurrentStateInternal")]
    public static class Patch_Greedy_CurrentStateInternal
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            Building_Bed building_Bed = p.ownership.OwnedBed;
            if (building_Bed == null) return;
            var modExt = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (modExt == null) return;

            var effect = building_Bed.def.GetModExtension<SecondFloorModExtension>();
            if (effect == null) return;
            if (effect.RemoveGreedyWant) __result = false;
        }
    }


    // Remove the ideoligion forbidment for second floor beds
    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "IdeoligionForbids")]
    public class CompAssignableToPawn_Bed_IdeoligionForbids
    {
        public static void Postfix(CompAssignableToPawn_Bed __instance, ref bool __result, Pawn pawn)
        {
            if (__instance?.parent == null) return;
            var modExt = __instance.parent.def.GetModExtension<SecondFloorModExtension>();
            if (modExt == null) return;

            var effect = __instance.parent.def.GetModExtension<SecondFloorModExtension>();
            if (effect == null) return;
            if (effect.ideologySecondFloorAssignmentAllowed) __result = false;
        }
    }

    // Modify the bed's sleeping slots count to match the bedCount if it has CompMultipleBeds
    [HarmonyPatch(typeof(Building_Bed), "SleepingSlotsCount", MethodType.Getter)]
    public static class Building_Bed_SleepingSlotsCount
    {
        public static bool Prefix(Building_Bed __instance, ref int __result)
        {
            if (__instance == null)
            {
                return true;
            }

            var compMultipleBeds = __instance.GetComp<CompMultipleBeds>();
            if (compMultipleBeds == null)
            {
                return true;
            }
            var bedSpots = compMultipleBeds.bedCount;
            if (bedSpots < 1) 
            {
                return true;
            }
            __result = bedSpots;
            return false;
        }
    }

    // If index is outside of the bed size, return the bed center and skip the original method
    [HarmonyPatch(typeof(BedUtility), "GetSlotPos")]
    public static class BedUtility_GetSlotPos_Patch
    {
        public static bool Prefix(ref IntVec3 __result, int index, IntVec3 bedCenter, Rot4 bedRot, IntVec2 bedSize, bool head)
        {
            int sleepingSlotsCount = BedUtility.GetSleepingSlotsCount(bedSize);

            if (index < 0 || index >= sleepingSlotsCount)
            {
                __result = bedCenter;
                return false;
            }

            return true;
        }
    }

    // Postfix to set the pawn position to the bed position if the bed has CompMultipleBeds
    [HarmonyPatch(typeof(Toils_LayDown), "LayDown")]
    public static class Toils_LayDown_LayDown_Patch
    {
        public static void Postfix(Toil __result)
        {
            if (__result == null) return;

            Action originalInitAction = __result.initAction;
            __result.initAction = delegate
            {
                Pawn actor = __result.actor;
                if (actor != null)
                {
                    // Logic to fix position before the original check runs
                    Building_Bed bed = actor.CurrentBed();
                    if (bed != null && bed.GetComp<CompMultipleBeds>() != null)
                    {
                        actor.Position = bed.Position;
                    }
                }

                // Run original initAction
                originalInitAction?.Invoke();
            };
        }
    }

    // Prefix to assign pawns to the correct slot index
    [HarmonyPatch(typeof(Building_Bed), "GetCurOccupant")]
    public static class Building_Bed_GetCurOccupant_Patch
    {
        public static bool Prefix(Building_Bed __instance, ref Pawn __result, int slotIndex)
        {
            var bedSize = __instance.def.size.x;
            if (__instance.GetComp<CompMultipleBeds>() != null)
            {
                if ((bedSize == 2 && slotIndex > 0) || (bedSize == 1))
                {
                    __result = GetCurOccupant(__instance, slotIndex);
                    return false;
                }
            }
            return true;
        }

        public static Pawn GetCurOccupant(Building_Bed __instance, int slotIndex)
        {
            if (!__instance.Spawned)
            {
                return null;
            }
            IntVec3 sleepingSlotPos = __instance.Position;
            List<Thing> list = __instance.Map.thingGrid.ThingsListAt(sleepingSlotPos);
            var comp = __instance.CompAssignableToPawn;
            for (int i = 0; i < list.Count; i++)
            {
                Pawn pawn = list[i] as Pawn;
                if (pawn != null && pawn.CurJob != null && pawn.GetPosture().InBed())
                {
                    if (comp.AssignedPawnsForReading.IndexOf(pawn) == slotIndex)
                    {
                        return pawn;
                    }
                }
            }
            return null;
        }
    }

    // DrawGUIOverlay for multiple beds
    [HarmonyPatch]
    public static class Building_Bed_DrawGUIOverlay_Patch
    {
        public static Type guestBedType;
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Building_Bed), nameof(Building_Bed.DrawGUIOverlay));
            guestBedType = AccessTools.TypeByName("Hospitality.Building_GuestBed");
            if (guestBedType != null)
            {
                var guestMethod = AccessTools.Method(guestBedType, "DrawGUIOverlay");
                if (guestMethod != null)
                {
                    yield return guestMethod;
                }
            }
        }

        public static bool Prefix(Building_Bed __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            var compMultipleBeds = __instance.GetComp<CompMultipleBeds>();
            if (compMultipleBeds == null)
            {
                return true;
            }

            compMultipleBeds.DrawGUIOverlay();

            if (guestBedType != null && guestBedType.IsAssignableFrom(__instance.def.thingClass))
            {
                if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Closest)
                {
                    var rentalFee = (int)Traverse.Create(__instance).Field("rentalFee").GetValue();
                }
            }
            return false;
        }
    }

    // Hide pawn if they're in a second floor bed
    [HarmonyPatch(typeof(PawnUIOverlay), "DrawPawnGUIOverlay")]
    public static class PawnUIOverlay_DrawPawnGUIOverlay_Patch
    {
        public static bool Prefix(PawnUIOverlay __instance)
        {

            if (__instance == null)
            {
                return true;
            }

            var pawn = (Pawn)AccessTools.Field(typeof(PawnUIOverlay), "pawn").GetValue(__instance);
            if (pawn == null)
            {
                return true;
            }

            var bed = pawn.CurrentBed();
            if (bed == null)
            {
                return true;
            }

            var compMultipleBeds = bed.GetComp<CompMultipleBeds>();
            if (compMultipleBeds == null)
            {
                return true;
            }

            return false;
        }
    }
    // Increase the maxAssignedPawnsCount to match the bedCount if it has CompMultipleBeds
    [HarmonyPatch(typeof(CompProperties_AssignableToPawn), "PostLoadSpecial")]
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
    }

}
