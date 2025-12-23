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
        // Main window size - wider to accommodate the two-panel layout
        private static readonly Vector2 WinSize = new Vector2(600f, 520f);
        
        // Left panel (upgrade list) dimensions
        private const float LeftPanelWidth = 220f;
        private const float UpgradeRowHeight = 40f;
        private const float IconSize = 32f;
        
        // Right panel (details) dimensions  
        private const float RightPanelMargin = 10f;
        
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 detailsScrollPosition = Vector2.zero;
        private float detailsScrollHeight = 0f;
        private StaircaseUpgradeDef selectedUpgrade = null;

        // Cached textures for upgrade icons (will be loaded dynamically or use defaults)
        private static Texture2D defaultUpgradeIcon;
        private static Texture2D installedCheckmark;

        public ITab_Staircase()
        {
            this.size = WinSize;
            this.labelKey = "TabStaircase";
            this.tutorTag = "Staircase";
        }

        private static void LoadTextures()
        {
            if (defaultUpgradeIcon == null)
            {
                // Use a built-in texture as fallback
                defaultUpgradeIcon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false) ?? BaseContent.BadTex;
            }
            if (installedCheckmark == null)
            {
                installedCheckmark = ContentFinder<Texture2D>.Get("UI/Widgets/CheckOn", false) ?? BaseContent.BadTex;
            }
        }

        protected override void FillTab()
        {
            LoadTextures();
            
            CompStaircaseUpgrades comp = SelThing.TryGetComp<CompStaircaseUpgrades>();
            if (comp == null)
            {
                return;
            }

            Rect mainRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            
            // Draw header
            Rect headerRect = new Rect(mainRect.x, mainRect.y, mainRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "Staircase Details");
            Text.Font = GameFont.Small;
            
            // Draw stats below header
            float statsY = headerRect.yMax + 5f;
            Rect statsRect = new Rect(mainRect.x, statsY, mainRect.width, 80f);
            DrawStaircaseStats(statsRect, comp);
            
            // Main content area (below stats)
            float contentY = statsRect.yMax + 10f;
            Rect contentRect = new Rect(mainRect.x, contentY, mainRect.width, mainRect.yMax - contentY);
            
            // Left panel - upgrade list
            Rect leftPanel = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            DrawUpgradeList(leftPanel, comp);
            
            // Right panel - details (only if an upgrade is selected)
            if (selectedUpgrade != null)
            {
                Rect rightPanel = new Rect(leftPanel.xMax + RightPanelMargin, contentRect.y, 
                    contentRect.width - LeftPanelWidth - RightPanelMargin, contentRect.height);
                DrawUpgradeDetails(rightPanel, comp, selectedUpgrade);
            }
        }

        private void DrawStaircaseStats(Rect rect, CompStaircaseUpgrades comp)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            
            CompMultipleBeds bedsComp = SelThing.TryGetComp<CompMultipleBeds>();
            if (bedsComp != null)
            {
                listing.Label($"Bed Spaces: {bedsComp.bedCount}");
            }

            // Calculate impressiveness level and room type
            int impressivenessLevel = 1;
            string roomType = "Private Rooms";
            bool isBarracks = false;
            bool isBasement = false;

            CompProperties_GiveThoughtStairs thoughtComp = SelThing.TryGetComp<CompGiveThoughtStairs>()?.Props;
            if (thoughtComp != null && thoughtComp.thoughtDef != null)
            {
                isBasement = thoughtComp.thoughtDef.defName == "SF_Low_Quality_Basement";
            }

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

            string[] impressivenessLabels = new string[]
            {
                "Awful", "Dull", "Mediocre", "Decent", "Slightly Impressive",
                "Impressive", "Very Impressive", "Extremely Impressive",
                "Unbelievably Impressive", "Wondrously Impressive"
            };
            
            listing.Label($"Room Type: {roomType}");
            listing.Label($"Impressiveness: {impressivenessLabels[impressivenessLevel]}");
            
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            listing.Label($"Space: {usedSpace}/{totalSpace} (Available: {totalSpace - usedSpace})");
            
            listing.End();
        }

        private void DrawUpgradeList(Rect rect, CompStaircaseUpgrades comp)
        {
            // Draw background
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(4f);
            
            // Section label
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, "Upgrades");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Scrollable list area
            Rect scrollOuterRect = new Rect(rect.x, labelRect.yMax + 4f, rect.width, rect.height - labelRect.height - 8f);
            
            // Gather all applicable upgrades
            List<StaircaseUpgradeDef> allUpgrades = GetApplicableUpgrades(comp);
            List<StaircaseUpgradeDef> pendingUpgrades = GetPendingUpgrades(SelThing);
            
            float viewHeight = allUpgrades.Count * UpgradeRowHeight;
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var def in allUpgrades)
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, UpgradeRowHeight);
                bool isInstalled = comp.upgrades.Contains(def);
                bool isPending = pendingUpgrades.Contains(def);
                bool isSelected = selectedUpgrade == def;
                bool isLocked = IsUpgradeLocked(def, comp);
                
                DrawUpgradeRow(rowRect, def, isInstalled, isPending, isSelected, isLocked);
                curY += UpgradeRowHeight;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawUpgradeRow(Rect rect, StaircaseUpgradeDef def, bool isInstalled, bool isPending, bool isSelected, bool isLocked)
        {
            // Highlight if selected
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // Alternating background for readability
            if (!isSelected && !Mouse.IsOver(rect))
            {
                int index = DefDatabase<StaircaseUpgradeDef>.AllDefsListForReading.IndexOf(def);
                if (index % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rect);
                }
            }
            
            // Icon area
            Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            
            // Get icon for upgrade (use default if none available)
            Texture2D icon = GetUpgradeIcon(def);
            
            // Gray out locked upgrades
            if (isLocked)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            
            GUI.DrawTexture(iconRect, icon);
            
            // Draw installed checkmark overlay
            if (isInstalled)
            {
                Rect checkRect = new Rect(iconRect.xMax - 12f, iconRect.y, 14f, 14f);
                GUI.color = Color.green;
                GUI.DrawTexture(checkRect, installedCheckmark);
                GUI.color = Color.white;
            }
            
            // Label
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, rect.width - IconSize - 14f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Color based on status
            if (isLocked)
            {
                GUI.color = Color.gray;
            }
            else if (isInstalled)
            {
                GUI.color = Color.green;
            }
            else if (isPending)
            {
                GUI.color = Color.yellow;
            }
            
            string label = def.label;
            if (isPending)
            {
                label += " (pending)";
            }
            else if (isLocked)
            {
                label += " (locked)";
            }
            
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Handle click (don't allow selecting locked upgrades for building, but allow viewing them)
            if (Widgets.ButtonInvisible(rect))
            {
                selectedUpgrade = def;
            }
            
            // Tooltip
            string tooltip = def.description;
            if (isLocked)
            {
                tooltip += "\n\n<color=#ff6666>This upgrade requires other upgrades to be installed first.</color>";
            }
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private void DrawUpgradeDetails(Rect rect, CompStaircaseUpgrades comp, StaircaseUpgradeDef def)
        {
            // Draw background
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(8f);
            
            bool isInstalled = comp.upgrades.Contains(def);
            bool isPending = GetPendingUpgrades(SelThing).Contains(def);
            
            // Reserve space for buttons at bottom
            float buttonHeight = 35f;
            Rect scrollableRect = new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height - buttonHeight - 4f);
            Rect buttonRect = new Rect(innerRect.x, scrollableRect.yMax + 4f, innerRect.width, buttonHeight);
            
            // Scrollable content area
            Rect viewRect = new Rect(0f, 0f, scrollableRect.width - 16f, detailsScrollHeight);
            Widgets.BeginScrollView(scrollableRect, ref detailsScrollPosition, viewRect);
            
            float curY = 0f;
            
            // Title
            Rect titleRect = new Rect(0f, curY, viewRect.width, 28f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, def.label);
            Text.Font = GameFont.Small;
            curY += 28f + 6f;
            
            // Status indicator
            string status = isInstalled ? "INSTALLED" : (isPending ? "UNDER CONSTRUCTION" : "NOT INSTALLED");
            Color statusColor = isInstalled ? Color.green : (isPending ? Color.yellow : Color.gray);
            GUI.color = statusColor;
            Rect statusRect = new Rect(0f, curY, viewRect.width, 24f);
            Widgets.Label(statusRect, $"Status: {status}");
            GUI.color = Color.white;
            curY += 24f + 6f;
            
            // Description
            float descHeight = Text.CalcHeight(def.description, viewRect.width);
            Rect descRect = new Rect(0f, curY, viewRect.width, descHeight);
            Widgets.Label(descRect, def.description);
            curY += descHeight + 10f;
            
            // Buffs section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Effects:</b>");
            curY += 24f;
            curY = DrawUpgradeEffectsScrollable(0f, curY, viewRect.width, def);
            curY += 10f;
            
            // Costs section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Costs:</b>");
            curY += 24f;
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  Space: {def.spaceCost}");
            curY += 24f;
            
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                ThingDef buildingDef = def.upgradeBuildingDef;
                if (buildingDef.costList != null && buildingDef.costList.Count > 0)
                {
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  Materials:");
                    curY += 24f;
                    foreach (var cost in buildingDef.costList)
                    {
                        int available = SelThing.Map.resourceCounter.GetCount(cost.thingDef);
                        string colorTag = available >= cost.count ? "" : "<color=#ff6666>";
                        string colorEnd = available >= cost.count ? "" : "</color>";
                        Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"    {colorTag}{cost.thingDef.label}: {cost.count} (have: {available}){colorEnd}");
                        curY += 24f;
                    }
                }
            }
            curY += 10f;
            
            // Requirements section
            Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Requirements:</b>");
            curY += 24f;
            bool hasRequirements = false;
            
            // Required upgrades
            if (def.requiredUpgrades != null && def.requiredUpgrades.Count > 0)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  Required upgrades:");
                curY += 24f;
                foreach (var requiredUpgrade in def.requiredUpgrades)
                {
                    bool reqInstalled = comp.upgrades.Contains(requiredUpgrade);
                    string colorTag = reqInstalled ? "<color=#00ff00>" : "<color=#ff6666>";
                    string colorEnd = "</color>";
                    string reqStatus = reqInstalled ? "✓" : "✗";
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"    {colorTag}{reqStatus} {requiredUpgrade.label}{colorEnd}");
                    curY += 24f;
                }
                hasRequirements = true;
            }
            
            if (def.minConstructionSkill > 0)
            {
                bool hasSkilledPawn = SelThing.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Construction).Level >= def.minConstructionSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  {colorTag}Construction skill: {def.minConstructionSkill}{colorEnd}");
                curY += 24f;
                hasRequirements = true;
            }
            if (def.minArtisticSkill > 0)
            {
                bool hasSkilledPawn = SelThing.Map.mapPawns.FreeColonists.Any(p => 
                    p.skills.GetSkill(SkillDefOf.Artistic).Level >= def.minArtisticSkill);
                string colorTag = hasSkilledPawn ? "" : "<color=#ff6666>";
                string colorEnd = hasSkilledPawn ? "" : "</color>";
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  {colorTag}Artistic skill: {def.minArtisticSkill}{colorEnd}");
                curY += 24f;
                hasRequirements = true;
            }
            if (!hasRequirements)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "  None");
                curY += 24f;
            }
            curY += 10f;
            
            // Unlocks section - show what upgrades this one unlocks
            List<StaircaseUpgradeDef> unlocksUpgrades = GetUpgradesUnlockedBy(def);
            if (unlocksUpgrades.Count > 0)
            {
                Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), "<b>Unlocks:</b>");
                curY += 24f;
                foreach (var unlockedUpgrade in unlocksUpgrades)
                {
                    Widgets.Label(new Rect(0f, curY, viewRect.width, 24f), $"  • {unlockedUpgrade.label}");
                    curY += 24f;
                }
                curY += 10f;
            }
            
            // Store height for next frame
            detailsScrollHeight = curY;
            
            Widgets.EndScrollView();
            
            // Buttons at the bottom - outside scroll view
            if (isInstalled)
            {
                // Show Remove button
                if (Widgets.ButtonText(buttonRect, "Remove (75% refund)"))
                {
                    TryRemoveUpgrade(def, comp, SelThing);
                }
            }
            else if (!isPending)
            {
                // Show Build button
                float availableSpace = comp.GetTotalSpace() - comp.GetUsedSpace();
                bool canAffordSpace = availableSpace >= def.spaceCost;
                bool isLocked = IsUpgradeLocked(def, comp);
                bool canBuild = canAffordSpace && !isLocked;
                
                Color oldColor = GUI.color;
                if (!canBuild)
                {
                    GUI.color = Color.red;
                }
                
                if (Widgets.ButtonText(buttonRect, "Build Upgrade", active: canBuild))
                {
                    if (canBuild)
                    {
                        TryAddUpgrade(def, comp, SelThing);
                    }
                }
                GUI.color = oldColor;
                
                if (isLocked)
                {
                    TooltipHandler.TipRegion(buttonRect, "This upgrade is locked. Install the required upgrades first.");
                }
                else if (!canAffordSpace)
                {
                    TooltipHandler.TipRegion(buttonRect, $"Not enough space! Need {def.spaceCost}, have {availableSpace}");
                }
            }
            else
            {
                // Pending - show cancel option
                if (Widgets.ButtonText(buttonRect, "Cancel Construction"))
                {
                    TryCancelUpgrade(def, SelThing);
                }
            }
        }

        private float DrawUpgradeEffectsScrollable(float x, float y, float width, StaircaseUpgradeDef def)
        {
            float curY = y;
            bool hasEffects = false;
            
            if (def.bedCountOffset != 0)
            {
                string sign = def.bedCountOffset > 0 ? "+" : "";
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Bed count: {sign}{def.bedCountOffset}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.bedCountMultiplier != 1f)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Bed count multiplier: x{def.bedCountMultiplier}");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.removeSleepDisturbed)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  Removes sleep disturbed");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.impressivenessLevel != 0)
            {
                string sign = def.impressivenessLevel > 0 ? "+" : "";
                Widgets.Label(new Rect(x, curY, width, 24f), $"  Impressiveness: {sign}{def.impressivenessLevel} level(s)");
                curY += 24f;
                hasEffects = true;
            }
            
            if (def.thoughtReplacement != null)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  Changes room type thought");
                curY += 24f;
                hasEffects = true;
            }
            
            if (!hasEffects)
            {
                Widgets.Label(new Rect(x, curY, width, 24f), "  No gameplay effects");
                curY += 24f;
            }
            
            return curY;
        }

        private List<StaircaseUpgradeDef> GetApplicableUpgrades(CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> result = new List<StaircaseUpgradeDef>();
            
            foreach (var def in DefDatabase<StaircaseUpgradeDef>.AllDefs)
            {
                // Skip if not applicable to this staircase type
                if (def.applyToStairs != null && !def.applyToStairs.Contains(SelThing.def))
                {
                    continue;
                }
                result.Add(def);
            }
            
            return result;
        }

        private Texture2D GetUpgradeIcon(StaircaseUpgradeDef def)
        {
            // Priority 1: Use the texture defined on the upgrade def itself
            if (def.Icon != null)
            {
                return def.Icon;
            }
            
            // Priority 2: Use the LAST item from the cost list (usually the rarest material)
            List<ThingDefCountClass> costs = null;
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                costs = def.upgradeBuildingDef.costList;
            }
            else if (def.costList != null)
            {
                costs = def.costList;
            }
            
            if (costs != null && costs.Count > 0)
            {
                ThingDef lastCostItem = costs[costs.Count - 1].thingDef;
                if (lastCostItem?.uiIcon != null)
                {
                    return lastCostItem.uiIcon;
                }
            }
            
            // Priority 3: Use default power icon (for instant upgrades with no costs)
            return defaultUpgradeIcon;
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

            // Store bed count before the upgrade
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                PlaceUpgradeBlueprint(def, staircase);
            }
            else
            {
                // Instant upgrade (legacy behavior)
                comp.upgrades.Add(def);
                
                // Check bed count after upgrade
                int bedCountAfter = bedsComp?.bedCount ?? 0;
                if (bedCountAfter < bedCountBefore)
                {
                    CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade installation");
                }
                
                Messages.Message("SF_UpgradeInstalled".Translate(def.label, staircase.Label), 
                    staircase, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void TryRemoveUpgrade(StaircaseUpgradeDef def, CompStaircaseUpgrades comp, Thing staircase)
        {
            if (!comp.upgrades.Contains(def))
            {
                return;
            }

            // Check if this upgrade is required by any other installed upgrade
            List<StaircaseUpgradeDef> dependentUpgrades = GetInstalledUpgradesThatRequire(def, comp);
            if (dependentUpgrades.Count > 0)
            {
                string dependentNames = string.Join(", ", dependentUpgrades.Select(u => u.label));
                Messages.Message($"Cannot remove {def.label}: It is required by {dependentNames}", staircase, MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Store bed count before removal
            CompMultipleBeds bedsComp = staircase.TryGetComp<CompMultipleBeds>();
            int bedCountBefore = bedsComp?.bedCount ?? 0;

            // Remove the upgrade
            comp.upgrades.Remove(def);

            // Refund 75% of materials
            if (def.RequiresConstruction && def.upgradeBuildingDef != null)
            {
                RefundMaterials(def.upgradeBuildingDef, staircase, 0.75f);
            }

            // Check bed count after removal
            int bedCountAfter = bedsComp?.bedCount ?? 0;
            if (bedCountAfter < bedCountBefore)
            {
                CheckAndResetBedAssignments(staircase, bedCountAfter, "upgrade removal");
            }

            Messages.Message("SF_UpgradeRemoved".Translate(def.label), staircase, MessageTypeDefOf.NeutralEvent, false);
            
            // Clear selection since the upgrade is gone
            selectedUpgrade = null;
        }

        private void RefundMaterials(ThingDef buildingDef, Thing staircase, float refundPercent)
        {
            if (buildingDef.costList == null || buildingDef.costList.Count == 0)
            {
                return;
            }

            foreach (var cost in buildingDef.costList)
            {
                int refundAmount = Mathf.FloorToInt(cost.count * refundPercent);
                if (refundAmount > 0)
                {
                    Thing refundThing = ThingMaker.MakeThing(cost.thingDef);
                    refundThing.stackCount = refundAmount;
                    
                    // Try to spawn near the staircase
                    IntVec3 dropPos = staircase.Position;
                    GenPlace.TryPlaceThing(refundThing, dropPos, staircase.Map, ThingPlaceMode.Near);
                }
            }
        }

        private void CheckAndResetBedAssignments(Thing staircase, int newBedCount, string reason)
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

        private void TryCancelUpgrade(StaircaseUpgradeDef def, Thing staircase)
        {
            
            Map map = staircase.Map;
            CellRect staircaseRect = staircase.OccupiedRect();

            // Find and destroy blueprints/frames for this upgrade
            List<Thing> toDestroy = new List<Thing>();
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Blueprint blueprint = t as Blueprint;
                    if (blueprint != null)
                    {
                        ThingDef blueprintBuildDef = blueprint.def.entityDefToBuild as ThingDef;
                        var ext = blueprintBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef == def)
                        {
                            toDestroy.Add(t);
                        }
                    }
                }
            }
            
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (staircaseRect.Contains(t.Position))
                {
                    Frame frame = t as Frame;
                    if (frame != null)
                    {
                        ThingDef frameBuildDef = frame.def.entityDefToBuild as ThingDef;
                        var ext = frameBuildDef?.GetModExtension<StaircaseUpgradeExtension>();
                        if (ext?.upgradeDef == def)
                        {
                            toDestroy.Add(t);
                        }
                    }
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

        private void PlaceUpgradeBlueprint(StaircaseUpgradeDef upgradeDef, Thing staircase)
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
                null
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

        private bool IsUpgradeLocked(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
        {
            if (def.requiredUpgrades == null || def.requiredUpgrades.Count == 0)
            {
                return false;
            }

            foreach (var requiredUpgrade in def.requiredUpgrades)
            {
                if (!comp.upgrades.Contains(requiredUpgrade))
                {
                    return true;
                }
            }

            return false;
        }

        private List<StaircaseUpgradeDef> GetUpgradesUnlockedBy(StaircaseUpgradeDef def)
        {
            List<StaircaseUpgradeDef> unlockedUpgrades = new List<StaircaseUpgradeDef>();
            
            foreach (var otherDef in DefDatabase<StaircaseUpgradeDef>.AllDefsListForReading)
            {
                if (otherDef.requiredUpgrades != null && otherDef.requiredUpgrades.Contains(def))
                {
                    unlockedUpgrades.Add(otherDef);
                }
            }
            
            return unlockedUpgrades;
        }

        private List<StaircaseUpgradeDef> GetInstalledUpgradesThatRequire(StaircaseUpgradeDef def, CompStaircaseUpgrades comp)
        {
            List<StaircaseUpgradeDef> dependentUpgrades = new List<StaircaseUpgradeDef>();
            
            foreach (var installedUpgrade in comp.upgrades)
            {
                if (installedUpgrade.requiredUpgrades != null && installedUpgrade.requiredUpgrades.Contains(def))
                {
                    dependentUpgrades.Add(installedUpgrade);
                }
            }
            
            return dependentUpgrades;
        }
    }
}
