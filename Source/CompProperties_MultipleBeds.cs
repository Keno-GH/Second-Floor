using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    public class CompProperties_MultipleBeds : CompProperties
    {
        public int bedCount = 1;

        public CompProperties_MultipleBeds()
        {
            this.compClass = typeof(CompMultipleBeds);
        }
    }

    public class CompMultipleBeds : ThingComp
    {
        public static HashSet<ThingWithComps> multipleBeds = new HashSet<ThingWithComps>();

        public CompProperties_MultipleBeds Props => props as CompProperties_MultipleBeds;

        public int bedCount
        {
            get
            {
                float count = Props.bedCount;
                var upgradesComp = parent.GetComp<CompStaircaseUpgrades>();
                if (upgradesComp != null)
                {
                    foreach (var upgrade in upgradesComp.upgrades)
                    {
                        count += upgrade.bedCountOffset;
                    }
                    foreach (var upgrade in upgradesComp.upgrades)
                    {
                        count *= upgrade.bedCountMultiplier;
                    }
                }
                return (int)count;
            }
        }

        public Graphic bedCounts;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            multipleBeds.Add(this.parent);
        }

        public void PostDeSpawn(Map map)
        {
            multipleBeds.Remove(this.parent);
        }

        public Vector3 GetDrawOffsetForPawns(int bedCount, Vector3 drawPos)
        {
            drawPos.y += 0.5f + bedCount + 1;
            return drawPos;
        }

        public override void DrawGUIOverlay()
        {

            base.DrawGUIOverlay();
            var bed = this.parent as Building_Bed;

            if (bed == null)
            {
                return;
            }

            if (bed.Medical || Find.CameraDriver.CurrentZoom != 0)
            {
                return;
            }
            Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
            if (!bed.OwnersForReading.Any() && (Building_Bed_DrawGUIOverlay_Patch.guestBedType is null
                || Building_Bed_DrawGUIOverlay_Patch.guestBedType.IsAssignableFrom(this.parent.def.thingClass) is false))
            {
                GenMapUI.DrawThingLabel(bed, "Unowned".Translate(), defaultThingLabelColor);
                return;
            }
            if (bed.OwnersForReading.Count == 1)
            {
                Pawn pawn = bed.OwnersForReading[0];
                pawn.CurrentBed(out var sleepingSpot);
                if ((!pawn.InBed() || pawn.CurrentBed() != bed || sleepingSpot == 0) && (!pawn.RaceProps.Animal || Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn)))
                {
                    GenMapUI.DrawThingLabel(this.parent, pawn.LabelShort, defaultThingLabelColor);
                }
                return;
            }
            for (int i = 0; i < bed.OwnersForReading.Count; i++)
            {
                Pawn pawn2 = bed.OwnersForReading[i];
                GenMapUI.DrawThingLabel(GetMultiOwnersLabelScreenPosFor(i, bed), pawn2.LabelShort, defaultThingLabelColor);
            }
        }

        private Vector3 GetMultiOwnersLabelScreenPosFor(int slotIndex, Building_Bed bed)
        {
            Vector3 drawPos = this.parent.DrawPos;
            var result = this.GetDrawOffsetForLabels(slotIndex, drawPos, bed).MapToUIPosition();
            return result;
        }

        public Vector3 GetDrawOffsetForLabels(int bedCount, Vector3 drawPos, Building_Bed bed)
        {

            var max_labels = bed.GetComp<CompMultipleBeds>().bedCount;
            float step_size = 0.4f;
            float z_value = (0.2f) + step_size * bedCount - 1;
            drawPos += new Vector3(0, 0, z_value);
            return drawPos;
        }
    }
}
