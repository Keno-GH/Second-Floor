using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace SecondFloor
{
    public class ITab_Staircase : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(420f, 480f);

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
            // bedCount=2: Single bedroom with 2 bedspaces (shared bed)
            // bedCount>=4: Multiple private rooms (2+ bedrooms, no shared bed debuff)
            // Barracks upgrade: Forces barracks type
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
            listing.Label("Upgrades:");
            
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

            listing.Gap();
            listing.Label("Available Upgrades:");
            
            foreach (var def in DefDatabase<StaircaseUpgradeDef>.AllDefs)
            {
                if (!comp.upgrades.Contains(def))
                {
                    if (def.applyToStairs != null && !def.applyToStairs.Contains(SelThing.def))
                    {
                        continue;
                    }

                    Rect buttonRect = listing.GetRect(Text.LineHeight);
                    if (Widgets.ButtonText(buttonRect, $"Add {def.label} (Cost: {def.spaceCost} space)"))
                    {
                        if (totalSpace - usedSpace >= def.spaceCost)
                        {
                            comp.upgrades.Add(def);
                        }
                        else
                        {
                            Messages.Message("Not enough space!", MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    if (!string.IsNullOrEmpty(def.description))
                    {
                        TooltipHandler.TipRegion(buttonRect, def.description);
                    }
                }
            }

            listing.End();
        }
    }
}
