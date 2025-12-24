using Verse;
using RimWorld;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// A building that represents a staircase upgrade construction project.
    /// When construction is completed and this building spawns, it immediately
    /// applies the upgrade to the parent staircase and despawns itself.
    /// 
    /// Uses BuildingOnTop altitude layer to coexist with the staircase.
    /// </summary>
    public class Building_StaircaseUpgrade : Building
    {
        // The upgrade def to apply - set via modExtension on the ThingDef
        private StaircaseUpgradeDef cachedUpgradeDef;

        public StaircaseUpgradeDef UpgradeDef
        {
            get
            {
                if (cachedUpgradeDef == null)
                {
                    var ext = def.GetModExtension<StaircaseUpgradeExtension>();
                    if (ext != null)
                    {
                        cachedUpgradeDef = ext.upgradeDef;
                    }
                }
                return cachedUpgradeDef;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                // Find the staircase at this position
                Thing staircase = FindStaircaseAtPosition(map);

                if (staircase != null)
                {
                    var upgradeComp = staircase.TryGetComp<CompStaircaseUpgrades>();
                    if (upgradeComp != null && UpgradeDef != null)
                    {
                        // Store bed count before the upgrade
                        var bedsComp = staircase.TryGetComp<CompMultipleBeds>();
                        int bedCountBefore = bedsComp?.bedCount ?? 0;

                        // Get the stuff this upgrade is made of (this building IS the upgrade being built)
                        ThingDef stuff = this.Stuff;
                        
                        // Apply the upgrade
                        if (!upgradeComp.HasUpgrade(UpgradeDef))
                        {
                            upgradeComp.AddUpgrade(UpgradeDef, stuff);
                            
                            // Check bed count after upgrade - may need to reset assignments
                            int bedCountAfter = bedsComp?.bedCount ?? 0;
                            if (bedCountAfter < bedCountBefore)
                            {
                                CheckAndResetBedAssignments(staircase, bedCountAfter);
                            }
                            
                            Messages.Message("SF_UpgradeInstalled".Translate(UpgradeDef.label, staircase.Label), 
                                staircase, MessageTypeDefOf.PositiveEvent, false);
                        }
                    }
                    else if (UpgradeDef == null)
                    {
                        Log.Error($"[SecondFloor] Building_StaircaseUpgrade spawned but has no UpgradeDef configured! ThingDef: {def.defName}");
                    }
                }
                else
                {
                    Log.Warning($"[SecondFloor] Building_StaircaseUpgrade spawned but no staircase found at position {Position}");
                }

                // Deselect the building before destroying it
                if (Find.Selector.IsSelected(this))
                {
                    Find.Selector.Deselect(this);
                }

                // Despawn this building - it's just a construction vehicle
                this.Destroy(DestroyMode.Vanish);
            }
        }

        private void CheckAndResetBedAssignments(Thing staircase, int newBedCount)
        {
            Building_Bed bed = staircase as Building_Bed;
            if (bed == null)
            {
                return;
            }

            System.Collections.Generic.List<Pawn> owners = bed.OwnersForReading.ToList();
            int currentAssignments = owners.Count;

            if (currentAssignments > newBedCount)
            {
                // Remove excess pawns from assignment (keep the first N pawns)
                for (int i = owners.Count - 1; i >= newBedCount; i--)
                {
                    Pawn pawn = owners[i];
                    pawn.ownership.UnclaimBed();
                }

                // Show message to player
                Messages.Message(
                    "SF_BedAssignmentsReset".Translate(bed.Label, newBedCount),
                    new LookTargets(bed),
                    MessageTypeDefOf.CautionInput,
                    false
                );
            }
        }

        private Thing FindStaircaseAtPosition(Map map)
        {
            // Look for any thing at this position that has the CompStaircaseUpgrades component
            foreach (Thing thing in Position.GetThingList(map).ToList())
            {
                if (thing != this && thing.TryGetComp<CompStaircaseUpgrades>() != null)
                {
                    return thing;
                }
            }
            return null;
        }

        private Thing FindUpgradeBuildingAtPosition(Map map)
        {
            // Look for any thing at this position that has the StaircaseUpgradeExtension mod extension
            foreach (Thing thing in Position.GetThingList(map).ToList())
            {
                if (thing != this)
                {
                    var ext = thing.def.GetModExtension<StaircaseUpgradeExtension>();
                    if (ext != null && ext.upgradeDef == UpgradeDef)
                    {
                        return thing;
                    }
                }
            }
            return null;
        }

        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();
            if (UpgradeDef != null)
            {
                if (!string.IsNullOrEmpty(baseStr))
                {
                    baseStr += "\n";
                }
                baseStr += "SF_UpgradeType".Translate(UpgradeDef.label);
            }
            return baseStr;
        }
    }

    /// <summary>
    /// ModExtension that links a staircase upgrade building ThingDef to its StaircaseUpgradeDef and settings.
    /// </summary>
    public class StaircaseUpgradeExtension : DefModExtension
    {
        public StaircaseUpgradeDef upgradeDef;
        public bool onePerBed = true; // By default, upgrades are one per bed
    }

    /// <summary>
    /// PlaceWorker that ensures upgrade buildings can only be placed on staircases,
    /// and prevents duplicate upgrades.
    /// </summary>
    public class PlaceWorker_OnStaircase : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            // Find a staircase at this location (any cell the staircase occupies)
            Thing staircase = null;
            foreach (Thing t in loc.GetThingList(map))
            {
                if (t.TryGetComp<CompStaircaseUpgrades>() != null)
                {
                    staircase = t;
                    break;
                }
            }

            if (staircase == null)
            {
                return new AcceptanceReport("SF_MustPlaceOnStaircase".Translate());
            }

            // Get the upgrade def from this building def
            ThingDef thingDef = checkingDef as ThingDef;
            if (thingDef != null)
            {
                var ext = thingDef.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.upgradeDef != null)
                {
                    var upgradeComp = staircase.TryGetComp<CompStaircaseUpgrades>();
                    
                    // Check if already has this upgrade
                    if (upgradeComp.HasUpgrade(ext.upgradeDef))
                    {
                        return new AcceptanceReport("SF_AlreadyHasUpgrade".Translate(ext.upgradeDef.label));
                    }

                    // Check if staircase type is allowed
                    if (ext.upgradeDef.applyToStairs != null && !ext.upgradeDef.applyToStairs.Contains(staircase.def))
                    {
                        return new AcceptanceReport("SF_UpgradeNotForThisStaircase".Translate());
                    }

                    // Check if there's enough space
                    float totalSpace = upgradeComp.GetTotalSpace();
                    float usedSpace = upgradeComp.GetUsedSpace();
                    if (totalSpace - usedSpace < ext.upgradeDef.spaceCost)
                    {
                        return new AcceptanceReport("SF_NotEnoughSpace".Translate(ext.upgradeDef.spaceCost, totalSpace - usedSpace));
                    }

                    // Check if there's already an upgrade under construction
                    foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
                    {
                        if (t.Position == loc || (staircase.OccupiedRect().Contains(t.Position)))
                        {
                            Blueprint blueprint = t as Blueprint;
                            if (blueprint != null)
                            {
                                ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                                if (blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>()?.upgradeDef == ext.upgradeDef)
                                {
                                    return new AcceptanceReport("SF_UpgradeAlreadyPlanned".Translate(ext.upgradeDef.label));
                                }
                            }
                        }
                    }

                    // Check frames too
                    foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
                    {
                        if (t.Position == loc || (staircase.OccupiedRect().Contains(t.Position)))
                        {
                            Frame frame = t as Frame;
                            if (frame != null)
                            {
                                ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                                if (frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>()?.upgradeDef == ext.upgradeDef)
                                {
                                    return new AcceptanceReport("SF_UpgradeUnderConstruction".Translate(ext.upgradeDef.label));
                                }
                            }
                        }
                    }
                }
            }

            return AcceptanceReport.WasAccepted;
        }

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            // Allow placing over staircases (they're in a different altitude layer anyway)
            return true;
        }
    }
}
