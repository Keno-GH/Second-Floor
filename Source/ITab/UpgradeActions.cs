using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// Static helper methods for upgrade actions (add, remove, toggle, etc.).
    /// </summary>
    public static class UpgradeActions
    {
        /// <summary>
        /// Attempts to add an upgrade to a staircase. Shows material selection menu if stuffable.
        /// </summary>
        public static void TryAddUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            float requiredSpace = comp.GetRequiredSpaceForUpgrade(def);

            if (totalSpace - usedSpace < requiredSpace)
            {
                Messages.Message("SF_NotEnoughSpaceMessage".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Store bed count before the upgrade
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Check if this upgrade is stuffable
            if (def.IsStuffable)
            {
                ShowStuffSelectionMenu(def, comp, staircase, bedCountBefore);
            }
            else
            {
                ApplyUpgrade(def, null, comp, staircase, bedCountBefore);
            }
        }
        
        /// <summary>
        /// Shows a float menu for selecting the material for a stuffable upgrade.
        /// </summary>
        public static void ShowStuffSelectionMenu(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase, int bedCountBefore)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCount = bedsComp?.bedCount ?? 1;
            
            int baseCost = 50;
            if (def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
            {
                baseCost = def.upgradeBuildingDef.costStuffCount;
            }
            
            int totalCost = baseCost * bedCount;
            
            IEnumerable<ThingDef> allowedStuffs = GenStuff.AllowedStuffsFor(def.upgradeBuildingDef, TechLevel.Undefined);
            
            foreach (ThingDef stuff in allowedStuffs)
            {
                var availableThings = staircase.Map.listerThings.ThingsOfDef(stuff)
                    .Where(t => !t.IsForbidden(Faction.OfPlayer));
                
                if (!availableThings.Any())
                {
                    continue;
                }
                
                int available = availableThings.Sum(t => t.stackCount);
                bool hasEnough = available >= totalCost;
                
                string label;
                if (hasEnough)
                {
                    label = $"{stuff.LabelCap} (Available: {available})";
                }
                else
                {
                    label = $"<color=#ff6666>{stuff.LabelCap} (Available: {available})</color>";
                }
                
                ThingDef stuffCopy = stuff;
                options.Add(new FloatMenuOption(label, delegate()
                {
                    ApplyUpgrade(def, stuffCopy, comp, staircase, bedCountBefore);
                }));
            }
            
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("No valid materials available on map", null));
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        /// <summary>
        /// Applies an upgrade to a staircase (places blueprint or instant apply).
        /// </summary>
        public static void ApplyUpgrade(StaircaseUpgradeDef def, ThingDef stuff, CompStaircaseUpgrades comp, Thing staircase, int bedCountBefore)
        {
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                if (def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>()?.onePerBed == true)
                {
                    int requiredCount = comp.GetRequiredBedCountForUpgrade(def);
                    
                    for (int i = 0; i < requiredCount; i++)
                    {
                        PlaceUpgradeBlueprint(def, stuff, staircase);
                    }
                }
                else
                {
                    PlaceUpgradeBlueprint(def, stuff, staircase);
                }
            }
            else
            {
                comp.AddUpgrade(def, stuff);
                
                if (def.IsStuffable && stuff != null)
                {
                    DeductMaterials(def, stuff, staircase);
                }
                
                CompMultipleBeds bedsComp2 = staircase.TryGetComp<CompMultipleBeds>();
                int bedCountAfter = bedsComp2?.bedCount ?? 0;
                if (bedCountAfter < bedCountBefore)
                {
                    CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade installation");
                }
                
                string materialInfo = stuff != null ? $" ({stuff.LabelCap})" : "";
                Messages.Message("SF_UpgradeInstalled".Translate(def.label + materialInfo, staircase.Label), 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }
        
        /// <summary>
        /// Deducts materials from the map for instant upgrades.
        /// </summary>
        private static void DeductMaterials(StaircaseUpgradeDef def, ThingDef stuff, Thing staircase)
        {
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCount = bedsComp?.bedCount ?? 1;
            
            int baseCost = 50;
            if (def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
            {
                baseCost = def.upgradeBuildingDef.costStuffCount;
            }
            
            int totalCost = baseCost * bedCount;
            
            List<Thing> toRemove = new List<Thing>();
            int remaining = totalCost;
            foreach (Thing thing in staircase.Map.listerThings.ThingsOfDef(stuff))
            {
                if (remaining <= 0) break;
                
                int toTake = Mathf.Min(thing.stackCount, remaining);
                thing.stackCount -= toTake;
                remaining -= toTake;
                
                if (thing.stackCount <= 0)
                {
                    toRemove.Add(thing);
                }
            }
            
            foreach (Thing thing in toRemove)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        /// <summary>
        /// Attempts to remove an upgrade from a staircase with refund.
        /// </summary>
        public static void TryRemoveUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase, ref StaircaseUpgradeDef selectedUpgrade)
        {
            if (!comp.HasUpgrade(def))
            {
                return;
            }

            List<StaircaseUpgradeDef> dependentUpgrades = UpgradeFiltering.GetInstalledUpgradesThatRequire(def, comp);
            if (dependentUpgrades.Count > 0)
            {
                string dependentNames = string.Join(", ", dependentUpgrades.Select(u => u.label));
                Messages.Message($"Cannot remove {def.label}: It is required by {dependentNames}", staircase, MessageTypeDefOf.RejectInput, false);
                return;
            }

            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            string refundInfo = comp.RemoveUpgradeWithRefund(def, 0.75f);

            int bedCountAfter = bedsComp?.bedCount ?? 0;
            if (bedCountAfter < bedCountBefore)
            {
                CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade removal");
            }

            Messages.Message("SF_UpgradeRemoved".Translate(def.label) + (refundInfo ?? ""), staircase, MessageTypeDefOf.NeutralEvent, false);
            
            selectedUpgrade = null;
        }
        
        /// <summary>
        /// Removes partially constructed upgrades with refund.
        /// </summary>
        public static void TryRemoveConstructedUpgrades(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            int constructedCount = comp.GetConstructedCount(def);
            if (constructedCount == 0)
            {
                return;
            }

            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            string refundInfo = comp.RemoveConstructedUpgradesWithRefund(def, 0.75f);

            int bedCountAfter = bedsComp?.bedCount ?? 0;
            if (bedCountAfter < bedCountBefore)
            {
                CheckAndResetBedAssignments(staircase, bedCountAfter, "constructed upgrade removal");
            }

            Messages.Message($"Removed {constructedCount} constructed {def.label} upgrade{(constructedCount > 1 ? "s" : "")}" + (refundInfo ?? ""), 
                staircase, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// Removes excess upgrades (more than needed for current bed count).
        /// </summary>
        public static void TryRemoveExcessUpgrades(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase, int excessCount)
        {
            if (excessCount <= 0)
            {
                return;
            }

            var (removed, refundInfo) = comp.RemoveExcessUpgradesWithRefund(def, excessCount, 0.75f);

            if (removed > 0)
            {
                Messages.Message($"Removed {removed} excess {def.label} upgrade{(removed > 1 ? "s" : "")}" + (refundInfo ?? ""), 
                    staircase, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>
        /// Resets bed assignments when bed count decreases.
        /// </summary>
        public static void CheckAndResetBedAssignments(Thing staircase, int newBedCount, string reason)
        {
            Building_Bed bed = staircase as Building_Bed;
            if (bed == null)
            {
                return;
            }

            List<Pawn> owners = bed.OwnersForReading.ToList();
            int currentAssignments = owners.Count;

            if (currentAssignments > newBedCount)
            {
                for (int i = owners.Count - 1; i >= newBedCount; i--)
                {
                    Pawn pawn = owners[i];
                    pawn.ownership.UnclaimBed();
                }

                Messages.Message(
                    "SF_BedAssignmentsReset".Translate(bed.Label, newBedCount),
                    new LookTargets(bed),
                    MessageTypeDefOf.CautionInput,
                    false
                );
            }
        }

        /// <summary>
        /// Cancels pending blueprints/frames for an upgrade.
        /// </summary>
        public static void TryCancelUpgrade(StaircaseUpgradeDef def, Thing staircase)
        {
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            List<Thing> toDestroy = new List<Thing>();
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Blueprint blueprint = t as Blueprint;
                if (blueprint == null)
                {
                    continue;
                }
                
                ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef == def)
                {
                    toDestroy.Add(t);
                }
            }
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (!staircaseRect.Contains(t.Position))
                {
                    continue;
                }
                
                Frame frame = t as Frame;
                if (frame == null)
                {
                    continue;
                }
                
                ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef == def)
                {
                    toDestroy.Add(t);
                }
            }

            foreach (Thing t in toDestroy)
            {
                t.Destroy(DestroyMode.Cancel);
            }

            if (toDestroy.Count > 0)
            {
                Messages.Message("SF_UpgradeCancelled".Translate(def.label), staircase, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>
        /// Places a blueprint for an upgrade construction.
        /// </summary>
        public static void PlaceUpgradeBlueprint(StaircaseUpgradeDef upgradeDef, ThingDef stuff, Thing staircase)
        {
            ThingDef buildingDef = upgradeDef.upgradeBuildingDef;
            if (buildingDef == null || buildingDef.blueprintDef == null)
            {
                return;
            }

            Map map = staircase.Map;
            IntVec3 position = staircase.Position;
            Rot4 rotation = staircase.Rotation;

            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(
                buildingDef, 
                position, 
                map, 
                rotation, 
                Faction.OfPlayer, 
                stuff
            );

            if (blueprint != null)
            {
                Messages.Message("SF_UpgradePlanned".Translate(upgradeDef.label), 
                    blueprint, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("SF_CannotPlaceBlueprint".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// Places missing blueprints for onePerBed upgrades.
        /// </summary>
        public static void FillMissingBlueprints(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            int requiredCount = comp.GetRequiredBedCountForUpgrade(def);
            int constructedCount = comp.GetConstructedCount(def);
            int pendingCount = UpgradeFiltering.GetPendingUpgradeCount(staircase, def);
            int needed = requiredCount - constructedCount - pendingCount;
            
            if (needed <= 0)
            {
                if (pendingCount > 0)
                {
                    Messages.Message($"{def.label}: Already have enough blueprints pending ({constructedCount} built + {pendingCount} pending = {constructedCount + pendingCount}, need {requiredCount})", 
                        staircase, MessageTypeDefOf.RejectInput, false);
                }
                return;
            }
            
            ActiveUpgrade activeUpgrade = comp.constructedUpgrades.FirstOrDefault(au => au.def == def);
            ThingDef stuff = activeUpgrade?.stuff;
            
            for (int i = 0; i < needed; i++)
            {
                PlaceUpgradeBlueprint(def, stuff, staircase);
            }
            
            Messages.Message($"Placed {needed} blueprints to complete {def.label}", 
                staircase, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>
        /// Dev mode: instantly applies an upgrade without materials or construction.
        /// </summary>
        public static void DevModeInstantUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            if (!comp.HasUpgrade(def))
            {
                comp.AddUpgrade(def, null);
                
                int bedCountAfter = bedsComp?.bedCount ?? 0;
                if (bedCountAfter < bedCountBefore)
                {
                    CheckAndResetBedAssignments(staircase, bedCountAfter, "dev mode upgrade");
                }
                
                Messages.Message($"[DEV] {def.label} instantly applied to {staircase.Label}", 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                comp.IncreaseUpgradeCount(def);
                Messages.Message($"[DEV] Increased {def.label} upgrade count on {staircase.Label}", 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
