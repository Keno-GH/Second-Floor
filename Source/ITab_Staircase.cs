using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecondFloor
{
    public class ITab_Staircase : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(420f, 520f);
        private Vector2 scrollPosition = Vector2.zero;

        public ITab_Staircase()
        {
            this.size = WinSize;
            this.labelKey = "TabStaircase";
            this.tutorTag = "Staircase";
        }

        protected override void FillTab()
        {
            CompStaircaseUpgrades comp = SelThing.TryGetComp<CompStaircaseUpgrades>();
            if (comp == null)
            {
                return;
            }

            Rect rect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            Rect rect2 = rect;
            rect2.height = 30f;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect2, "Staircase Details");
            Text.Font = GameFont.Small;

            Rect rect3 = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect3);
            
            CompMultipleBeds bedsComp = SelThing.TryGetComp<CompMultipleBeds>();
            if (bedsComp != null)
            {
                listing.Label($"Bed Spaces: {bedsComp.bedCount}");
            }

            // Calculate impressiveness level and room type
            int impressivenessLevel = 1; // Default "dull"
            string roomType = "Private Rooms";
            bool isBarracks = false;
            bool isBasement = false;

            // Check if it's a basement
            CompProperties_GiveThoughtStairs thoughtComp = SelThing.TryGetComp<CompGiveThoughtStairs>()?.Props;
            if (thoughtComp != null && thoughtComp.thoughtDef != null)
            {
                isBasement = thoughtComp.thoughtDef.defName == "SF_Low_Quality_Basement";
            }

            // Calculate impressiveness from upgrades
            int totalImpressivenessBonus = 0;
            foreach (var upgrade in comp.upgrades)
            {
                totalImpressivenessBonus += upgrade.impressivenessLevel;
                if (upgrade.defName == "SF_StaircaseUpgrade_Barracks")
                {
                    isBarracks = true;
                }
            }
            impressivenessLevel = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);

            // Determine room type description
            if (isBasement)
            {
                roomType = isBarracks ? "Basement Barracks" : "Basement";
            }
            else
            {
                if (isBarracks)
                {
                    roomType = "Barracks";
                }
                else if (bedsComp != null && bedsComp.bedCount >= 4)
                {
                    roomType = "Multiple Private Rooms";
                }
                else
                {
                    roomType = "Single Bedroom";
                }
            }

            // Display impressiveness and room type
            string[] impressivenessLabels = new string[]
            {
                "Awful",
                "Dull",
                "Mediocre",
                "Decent",
                "Slightly Impressive",
                "Impressive",
                "Very Impressive",
                "Extremely Impressive",
                "Unbelievably Impressive",
                "Wondrously Impressive"
            };
            
            listing.Label($"Room Type: {roomType}");
            listing.Label($"Impressiveness: {impressivenessLabels[impressivenessLevel]}");

            listing.Gap();

            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            
            listing.Label($"Total Space: {totalSpace}");
            listing.Label($"Used Space: {usedSpace}");
            listing.Label($"Available Space: {totalSpace - usedSpace}");
            
            listing.Gap();
            listing.Label("Installed Upgrades:");
            
            if (comp.upgrades.Count == 0)
            {
                listing.Label("None");
            }
            else
            {
                foreach (var upgrade in comp.upgrades)
                {
                    listing.Label($"- {upgrade.label}");
                }
            }

            // Show pending upgrades (blueprints and frames)
            List<StaircaseUpgradeDef> pendingUpgrades = GetPendingUpgrades(SelThing);
            if (pendingUpgrades.Count > 0)
            {
                listing.Gap(6f);
                listing.Label("Pending Upgrades:");
                foreach (var upgrade in pendingUpgrades)
                {
                    listing.Label($"- {upgrade.label} (under construction)");
                }
            }

            listing.Gap();
            listing.Label("Available Upgrades:");
            
            float availableSpace = totalSpace - usedSpace;
            
            foreach (var def in DefDatabase<StaircaseUpgradeDef>.AllDefs)
            {
                // Skip if already installed
                if (comp.upgrades.Contains(def))
                {
                    continue;
                }

                // Skip if already pending
                if (pendingUpgrades.Contains(def))
                {
                    continue;
                }

                // Skip if not applicable to this staircase type
                if (def.applyToStairs != null && !def.applyToStairs.Contains(SelThing.def))
                {
                    continue;
                }

                // Build button text and tooltip
                StringBuilder buttonText = new StringBuilder();
                buttonText.Append($"Add {def.label}");
                
                StringBuilder tooltip = new StringBuilder();
                tooltip.AppendLine(def.description);
                tooltip.AppendLine();
                tooltip.AppendLine($"Space cost: {def.spaceCost}");

                bool canAffordSpace = availableSpace >= def.spaceCost;
                bool canAffordMaterials = true;
                bool hasSkill = true;

                // Check if this upgrade requires construction
                if (def.RequiresConstruction && def.upgradeBuildingDef != null)
                {
                    // Get cost info from the building def
                    ThingDef buildingDef = def.upgradeBuildingDef;
                    
                    // Show material costs
                    if (buildingDef.costList != null && buildingDef.costList.Count > 0)
                    {
                        tooltip.AppendLine();
                        tooltip.AppendLine("Materials:");
                        foreach (var cost in buildingDef.costList)
                        {
                            int available = SelThing.Map.resourceCounter.GetCount(cost.thingDef);
                            tooltip.AppendLine($"  {cost.thingDef.label}: {cost.count} (have: {available})");
                            if (available < cost.count)
                            {
                                canAffordMaterials = false;
                            }
                        }
                    }

                    // Show work amount
                    float workAmount = buildingDef.GetStatValueAbstract(StatDefOf.WorkToBuild);
                    if (workAmount > 0)
                    {
                        tooltip.AppendLine($"Work to build: {workAmount}");
                    }

                    // Show skill requirement
                    if (def.minConstructionSkill > 0)
                    {
                        tooltip.AppendLine($"Requires construction skill: {def.minConstructionSkill}");
                        
                        // Check if any colonist can build it
                        hasSkill = SelThing.Map.mapPawns.FreeColonists.Any(p => 
                            p.skills.GetSkill(SkillDefOf.Construction).Level >= def.minConstructionSkill);
                        
                        if (!hasSkill)
                        {
                            tooltip.AppendLine("(No colonist has required skill!)");
                        }
                    }
                }

                // Only space is a hard requirement - materials and skill are just warnings
                // (vanilla behavior: you can place blueprints even without materials)
                bool canBuild = canAffordSpace;
                
                if (!canAffordSpace)
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine($"Not enough space! Need {def.spaceCost}, have {availableSpace}");
                }

                if (!canAffordMaterials)
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine("(Missing materials - colonists will wait for resources)");
                }

                if (!hasSkill)
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine("(No colonist can build this yet)");
                }

                Rect buttonRect = listing.GetRect(Text.LineHeight + 4f);
                
                // Draw the button - only space blocks placement, materials/skill just show warnings
                if (Widgets.ButtonText(buttonRect, buttonText.ToString(), active: canBuild))
                {
                    if (canBuild)
                    {
                        TryAddUpgrade(def, comp, SelThing);
                    }
                }
                
                TooltipHandler.TipRegion(buttonRect, tooltip.ToString());
            }

            listing.End();
        }

        private void TryAddUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();

            if (totalSpace - usedSpace < def.spaceCost)
            {
                Messages.Message("SF_NotEnoughSpaceMessage".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Check if this upgrade requires construction
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                // Place a blueprint for the upgrade building
                PlaceUpgradeBlueprint(def, staircase);
            }
            else
            {
                // Instant upgrade (1.5 legacy behavior)
                comp.upgrades.Add(def);
                Messages.Message("SF_UpgradeInstalled".Translate(def.label, staircase.Label), 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void PlaceUpgradeBlueprint(StaircaseUpgradeDef upgradeDef, Thing staircase)
        {
            ThingDef buildingDef = upgradeDef.upgradeBuildingDef;
            if (buildingDef == null || buildingDef.blueprintDef == null)
            {
                Log.Error($"[SecondFloor] Cannot place blueprint for {upgradeDef.defName}: no valid building def or blueprint def");
                return;
            }

            Map map = staircase.Map;
            IntVec3 position = staircase.Position;
            Rot4 rotation = staircase.Rotation;

            // Use vanilla blueprint placement
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(
                buildingDef, 
                position, 
                map, 
                rotation, 
                Faction.OfPlayer, 
                null // stuff
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

        private List<StaircaseUpgradeDef> GetPendingUpgrades(Thing staircase)
        {
            List<StaircaseUpgradeDef> pending = new List<StaircaseUpgradeDef>();
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            // Check blueprints
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Blueprint blueprint = t as Blueprint;
                    if (blueprint != null)
                    {
                        ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                        var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef != null)
                        {
                            pending.Add(ext.upgradeDef);
                        }
                    }
                }
            }

            // Check frames
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Frame frame = t as Frame;
                    if (frame != null)
                    {
                        ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                        var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef != null && !pending.Contains(ext.upgradeDef))
                        {
                            pending.Add(ext.upgradeDef);
                        }
                    }
                }
            }

            return pending;
        }
    }
}
