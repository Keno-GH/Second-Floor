using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace SecondFloor
{
    /// <summary>
    /// Reasons why an upgrade might be disabled
    /// </summary>
    public enum UpgradeDisableReason
    {
        None,
        ToggledOff,         // Manually toggled off by player
        OutOfFuel,
        NoPower,
        InsufficientCount,  // For onePerBed upgrades
        ReachedTemperature  // Controllable fueled temp changer is idle because target temp is reached
        // Future: Add more reasons here as needed
    }
    public class CompProperties_StaircaseUpgrades : CompProperties
    {
        /// <summary>
        /// List of upgrade defs that are automatically installed when the staircase spawns.
        /// These upgrades don't consume space and are typically marked as canRemove=false.
        /// </summary>
        public List<StaircaseUpgradeDef> initialUpgrades;
        
        public CompProperties_StaircaseUpgrades()
        {
            this.compClass = typeof(CompStaircaseUpgrades);
        }
    }
    
    /// <summary>
    /// Wrapper class for storing an upgrade along with the stuff it was built from
    /// </summary>
    public class ActiveUpgrade : IExposable
    {
        public StaircaseUpgradeDef def;
        public ThingDef stuff;
        public int count;
        /// <summary>
        /// Whether this upgrade has been manually toggled off by the player.
        /// Only applicable to upgrades that require fuel or power.
        /// </summary>
        public bool isToggledOff;
        
        public ActiveUpgrade()
        {
        }
        
        public ActiveUpgrade(StaircaseUpgradeDef def, ThingDef stuff)
        {
            this.def = def;
            this.stuff = stuff;
            this.count = 1;
            this.isToggledOff = false;
        }
        
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref isToggledOff, "isToggledOff", false);
        }
    }

    public partial class CompStaircaseUpgrades : ThingComp
    {
        public List<ActiveUpgrade> constructedUpgrades = new List<ActiveUpgrade>();
        private float cachedFuelConsumptionRate = 0f;
        
        // =====================================================
        // Cached Mod Extension (avoids repeated GetModExtension calls)
        // =====================================================
        private SecondFloorModExtension cachedModExtension;
        public SecondFloorModExtension ModExtension
        {
            get
            {
                if (cachedModExtension == null)
                {
                    cachedModExtension = parent.def.GetModExtension<SecondFloorModExtension>();
                }
                return cachedModExtension;
            }
        }
        
        // =====================================================
        // Bed Count System (merged from CompMultipleBeds)
        // =====================================================
        
        /// <summary>
        /// Static set tracking all staircases with multiple beds for global lookups.
        /// </summary>
        public static HashSet<ThingWithComps> multipleBeds = new HashSet<ThingWithComps>();
        
        private int cachedBedCount = -1;
        private bool bedCountDirty = true;
        
        /// <summary>
        /// Gets the current bed count, applying upgrade modifiers.
        /// Uses aggressive caching - only recalculates when upgrades change.
        /// </summary>
        public int BedCount
        {
            get
            {
                if (bedCountDirty || cachedBedCount < 0)
                {
                    RecalculateBedCount();
                }
                return cachedBedCount;
            }
        }
        
        /// <summary>
        /// Recalculates the bed count from base value + upgrade modifiers.
        /// Called when upgrades change.
        /// </summary>
        private void RecalculateBedCount()
        {
            float count = ModExtension?.bedCount ?? 1;
            
            // Apply offsets first, then multipliers
            foreach (var upgrade in constructedUpgrades)
            {
                count += upgrade.def.bedCountOffset;
            }
            foreach (var upgrade in constructedUpgrades)
            {
                count *= upgrade.def.bedCountMultiplier;
            }
            
            cachedBedCount = Mathf.Max(1, (int)count);
            bedCountDirty = false;
        }
        
        /// <summary>
        /// Invalidates the bed count cache. Called when upgrades are added/removed.
        /// Also updates CompAssignableToPawn.maxAssignedPawnsCount to allow more pawns to be assigned.
        /// </summary>
        public void InvalidateBedCountCache()
        {
            bedCountDirty = true;
            
            // Update maxAssignedPawnsCount to match the new bed count
            if (parent is Building_Bed bed)
            {
                var assignableComp = bed.GetComp<CompAssignableToPawn>();
                if (assignableComp != null)
                {
                    // Force recalculate bed count now so we have the correct value
                    RecalculateBedCount();
                    assignableComp.Props.maxAssignedPawnsCount = cachedBedCount;
                }
            }
        }
        
        // =====================================================
        // Smart Temperature Control System
        // =====================================================
        /// <summary>
        /// The target temperature for smart temperature modifiers (heaters, coolers, ACs).
        /// This is shared by all smart temp modifiers on this staircase.
        /// Default is 21°C (comfortable room temperature).
        /// </summary>
        public float targetTemperature = 21f;
        
        /// <summary>
        /// When true, fueled temperature changers (braziers, campfires) are used before electric ones.
        /// When false, electric temperature changers are prioritized.
        /// </summary>
        public bool preferFueledFirst = true;
        
        /// <summary>
        /// Cached power consumption value for display
        /// </summary>
        private float cachedTotalPowerConsumption = 0f;
        
        // =====================================================
        // Linked Battery System
        // =====================================================
        /// <summary>
        /// Reference to the linked battery building spawned by a battery upgrade.
        /// </summary>
        private Thing linkedBattery;
        
        // =====================================================
        // Linked Bathroom System (DBH Integration)
        // =====================================================
        /// <summary>
        /// Reference to the linked bathroom building spawned by a bathroom upgrade.
        /// </summary>
        private Thing linkedBathroom;
        
        // Legacy field for backward compatibility
        private List<StaircaseUpgradeDef> upgrades;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref constructedUpgrades, "constructedUpgrades", LookMode.Deep);
            Scribe_Values.Look(ref targetTemperature, "targetTemperature", 21f);
            Scribe_Values.Look(ref preferFueledFirst, "preferFueledFirst", true);
            Scribe_References.Look(ref linkedBattery, "linkedBattery");
            Scribe_References.Look(ref linkedBathroom, "linkedBathroom");
            
            // Partial class expose methods
            ExposeBasementData();
            ExposeBathroomData();
            
            // Legacy support: load old "upgrades" and "activeUpgrades" lists and convert to constructedUpgrades
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
                
                // Support old "activeUpgrades" field name
                List<ActiveUpgrade> oldActiveUpgrades = null;
                Scribe_Collections.Look(ref oldActiveUpgrades, "activeUpgrades", LookMode.Deep);
                if (oldActiveUpgrades != null && oldActiveUpgrades.Count > 0)
                {
                    if (constructedUpgrades == null)
                    {
                        constructedUpgrades = oldActiveUpgrades;
                    }
                }
            }
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (constructedUpgrades == null)
                {
                    constructedUpgrades = new List<ActiveUpgrade>();
                }
                
                // Convert legacy upgrades to constructedUpgrades
                if (upgrades != null && upgrades.Count > 0)
                {
                    foreach (var upgradeDef in upgrades)
                    {
                        if (!constructedUpgrades.Any(au => au.def == upgradeDef))
                        {
                            constructedUpgrades.Add(new ActiveUpgrade(upgradeDef, null));
                        }
                    }
                    upgrades = null; // Clear legacy data
                }
            }
        }
        
        public CompProperties_StaircaseUpgrades Props => (CompProperties_StaircaseUpgrades)props;
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // Track this staircase in the global multi-bed list
            multipleBeds.Add(this.parent);
            
            // Invalidate bed count cache on spawn
            InvalidateBedCountCache();
            
            // Don't add initial upgrades when loading a save
            if (respawningAfterLoad)
            {
                return;
            }
            
            // Add initial upgrades from comp properties
            if (Props.initialUpgrades.NullOrEmpty())
            {
                return;
            }
            
            foreach (var upgradeDef in Props.initialUpgrades)
            {
                if (upgradeDef == null)
                {
                    continue;
                }
                
                // Skip if already has this upgrade
                if (HasUpgrade(upgradeDef))
                {
                    continue;
                }
                
                // Handle stuff selection for stuffable upgrades
                ThingDef stuff = null;
                if (upgradeDef.IsStuffable && !upgradeDef.stuffCategories.NullOrEmpty())
                {
                    // Log a dev warning about stuffable initial upgrades
                    Log.Warning($"[SecondFloor] Initial upgrade '{upgradeDef.defName}' is stuffable. " +
                        "This may cause issues if the player wants to build more and the randomly selected material is unavailable. " +
                        "Consider making initial upgrades non-stuffable.");
                    
                    // Select a random allowed stuff
                    var allowedStuffs = GenStuff.AllowedStuffsFor(upgradeDef.upgradeBuildingDef);
                    if (allowedStuffs.Any())
                    {
                        stuff = allowedStuffs.RandomElement();
                    }
                }
                
                AddUpgrade(upgradeDef, stuff);
            }
        }
        
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            
            // Remove from global multi-bed tracking
            multipleBeds.Remove(this.parent);
        }
        
        public float GetTotalSpace()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return 0f;
            }
            
            // Basements use basement expansion for space calculation (base + bonus from mining)
            // BasementTotalSpace is defined in the Basement partial class
            if (ModExtension != null && ModExtension.HasBasementExpansion)
            {
                return BasementTotalSpace;
            }
            
            // Upstairs staircases count cells with constructed roofs in a circular area (radius 10)
            float count = 0f;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 10f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofConstructed)
                {
                    count += 1f;
                }
            }
            return count;
        }

        public float GetUsedSpace()
        {
            float used = 0;
            int rawBedCount = BedCount;
            
            foreach(var activeUpgrade in constructedUpgrades)
            {
                // Skip non-removable upgrades from space calculation (they're "free")
                if (!activeUpgrade.def.canRemove)
                {
                    continue;
                }
                
                used += activeUpgrade.def.spaceCost;
                
                // For spaceCostPerBed, apply barracks halving logic for ambient upgrades
                if (activeUpgrade.def.spaceCostPerBed > 0)
                {
                    int effectiveBedCount = GetEffectiveBedCountForUpgrade(activeUpgrade.def, rawBedCount);
                    used += activeUpgrade.def.spaceCostPerBed * effectiveBedCount;
                }
            }
            return used;
        }
        
        /// <summary>
        /// Gets the effective bed count for an upgrade, accounting for barracks halving of ambient upgrades.
        /// </summary>
        private int GetEffectiveBedCountForUpgrade(StaircaseUpgradeDef def, int rawBedCount)
        {
            if (def.upgradeBuildingDef == null)
            {
                return rawBedCount;
            }
            
            var ext = def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>();
            bool onePerBed = ext?.onePerBed ?? true;
            
            if (!onePerBed)
            {
                return 1; // Not a per-bed upgrade
            }
            
            bool directlyToBed = ext?.directlyToBed ?? false;
            if (!directlyToBed && IsBarracks && rawBedCount > 1)
            {
                return Mathf.CeilToInt(rawBedCount / 2f);
            }
            
            return rawBedCount;
        }
        
        /// <summary>
        /// Gets the required space for a new upgrade, accounting for barracks halving of ambient upgrades.
        /// Also includes additional space cost from existing per-bed upgrades when this upgrade increases bed count.
        /// </summary>
        public float GetRequiredSpaceForUpgrade(StaircaseUpgradeDef def)
        {
            int rawBedCount = BedCount;
            int effectiveBedCount = GetEffectiveBedCountForUpgrade(def, rawBedCount);
            float baseSpaceCost = def.spaceCost + (def.spaceCostPerBed * effectiveBedCount);
            
            // If this upgrade increases bed count, add space cost from existing per-bed upgrades
            float additionalSpaceCost = GetAdditionalSpaceCostFromBedIncrease(def);
            
            return baseSpaceCost + additionalSpaceCost;
        }
        
        /// <summary>
        /// Calculates the additional space cost from existing per-bed upgrades when a bed-increasing upgrade is added.
        /// </summary>
        public float GetAdditionalSpaceCostFromBedIncrease(StaircaseUpgradeDef def)
        {
            // Check if this upgrade increases bed count
            if (def.bedCountOffset <= 0 && def.bedCountMultiplier <= 1f)
            {
                return 0f;
            }
            
            int currentBedCount = BedCount;
            
            // Calculate what the new bed count would be after this upgrade
            int newBedCount = currentBedCount;
            newBedCount += def.bedCountOffset;
            newBedCount = (int)(newBedCount * def.bedCountMultiplier);
            
            if (newBedCount <= currentBedCount)
            {
                return 0f;
            }
            
            // Determine if this upgrade would make it a barracks (for future barracks halving logic)
            bool willBeBarracks = IsBarracks || def.defName == "SF_StaircaseUpgrade_Barracks";
            
            float additionalCost = 0f;
            
            // For each existing per-bed upgrade, calculate the additional space needed
            foreach (var activeUpgrade in constructedUpgrades)
            {
                if (activeUpgrade.def.spaceCostPerBed <= 0)
                {
                    continue;
                }
                
                // Get the effective bed count for this upgrade before and after
                int effectiveBefore = GetEffectiveBedCountForUpgradeWithBarracksOverride(activeUpgrade.def, currentBedCount, IsBarracks);
                int effectiveAfter = GetEffectiveBedCountForUpgradeWithBarracksOverride(activeUpgrade.def, newBedCount, willBeBarracks);
                
                int additionalBeds = effectiveAfter - effectiveBefore;
                if (additionalBeds > 0)
                {
                    additionalCost += activeUpgrade.def.spaceCostPerBed * additionalBeds;
                }
            }
            
            return additionalCost;
        }
        
        /// <summary>
        /// Gets a breakdown of additional space costs from bed increase for UI display.
        /// Returns a list of (upgrade label, additional space) tuples.
        /// </summary>
        public List<(string upgradeLabel, float additionalSpace)> GetAdditionalSpaceCostBreakdown(StaircaseUpgradeDef def)
        {
            var breakdown = new List<(string, float)>();
            
            // Check if this upgrade increases bed count
            if (def.bedCountOffset <= 0 && def.bedCountMultiplier <= 1f)
            {
                return breakdown;
            }
            
            int currentBedCount = BedCount;
            
            // Calculate what the new bed count would be after this upgrade
            int newBedCount = currentBedCount;
            newBedCount += def.bedCountOffset;
            newBedCount = (int)(newBedCount * def.bedCountMultiplier);
            
            if (newBedCount <= currentBedCount)
            {
                return breakdown;
            }
            
            // Determine if this upgrade would make it a barracks
            bool willBeBarracks = IsBarracks || def.defName == "SF_StaircaseUpgrade_Barracks";
            
            // For each existing per-bed upgrade, calculate the additional space needed
            foreach (var activeUpgrade in constructedUpgrades)
            {
                if (activeUpgrade.def.spaceCostPerBed <= 0)
                {
                    continue;
                }
                
                int effectiveBefore = GetEffectiveBedCountForUpgradeWithBarracksOverride(activeUpgrade.def, currentBedCount, IsBarracks);
                int effectiveAfter = GetEffectiveBedCountForUpgradeWithBarracksOverride(activeUpgrade.def, newBedCount, willBeBarracks);
                
                int additionalBeds = effectiveAfter - effectiveBefore;
                if (additionalBeds > 0)
                {
                    float additionalSpace = activeUpgrade.def.spaceCostPerBed * additionalBeds;
                    breakdown.Add((activeUpgrade.def.label, additionalSpace));
                }
            }
            
            return breakdown;
        }
        
        /// <summary>
        /// Gets the effective bed count for an upgrade with explicit barracks override.
        /// Used for calculating future states when an upgrade would change barracks status.
        /// </summary>
        private int GetEffectiveBedCountForUpgradeWithBarracksOverride(StaircaseUpgradeDef def, int rawBedCount, bool isBarracksOverride)
        {
            if (def.upgradeBuildingDef == null)
            {
                return rawBedCount;
            }
            
            var ext = def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>();
            bool onePerBed = ext?.onePerBed ?? true;
            
            if (!onePerBed)
            {
                return 1; // Not a per-bed upgrade
            }
            
            bool directlyToBed = ext?.directlyToBed ?? false;
            if (!directlyToBed && isBarracksOverride && rawBedCount > 1)
            {
                return Mathf.CeilToInt(rawBedCount / 2f);
            }
            
            return rawBedCount;
        }
        
        /// <summary>
        /// Helper method to check if an upgrade is constructed
        /// </summary>
        public bool HasUpgrade(StaircaseUpgradeDef def)
        {
            return constructedUpgrades.Any(au => au.def == def);
        }

        /// <summary>
        /// Helper method to check if an upgrade is active (valid)
        /// </summary>
        public bool HasActiveUpgrade(StaircaseUpgradeDef def)
        {
            return GetActiveUpgradeDefs().Contains(def);
        }
        
        /// <summary>
        /// Helper method to get all constructed upgrade defs (no validation).
        /// Used for bed count calculations to avoid circular dependency.
        /// </summary>
        public List<StaircaseUpgradeDef> GetConstructedUpgradeDefs()
        {
            return constructedUpgrades.Select(au => au.def).ToList();
        }
        
        /// <summary>
        /// Returns true if the staircase has the Barracks upgrade active.
        /// </summary>
        public bool IsBarracks
        {
            get
            {
                return constructedUpgrades.Any(au => au.def.defName == "SF_StaircaseUpgrade_Barracks");
            }
        }
        
        // =====================================================
        // Hospitality Integration - Virtual Bed Stats
        // =====================================================
        
        /// <summary>
        /// Struct containing virtual bed stats for Hospitality compatibility.
        /// These values are used instead of the physical room stats.
        /// </summary>
        public struct VirtualBedStats
        {
            public int impressiveness;
            public int quality;
            public int roomTypeScore;
            public int comfort;
            public int facilities;
            
            public int TotalValue => impressiveness + quality + roomTypeScore + comfort + facilities;
        }
        
        /// <summary>
        /// Calculates virtual bed stats based on upgrades for Hospitality integration.
        /// These values replace the physical room stats when calculating attractiveness.
        /// </summary>
        public VirtualBedStats CalculateVirtualBedStats()
        {
            var stats = new VirtualBedStats();
            
            // Calculate impressiveness from upgrade levels (0-9 stages)
            // Map to approximate RimWorld room impressiveness values
            int totalImpressivenessBonus = 0;
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                totalImpressivenessBonus += upgrade.impressivenessLevel;
            }
            int impressivenessStage = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);
            
            // Map impressiveness stage (0-9) to RimWorld impressiveness values
            // RimWorld room impressiveness typically ranges 0-150+
            switch (impressivenessStage)
            {
                case 0:  stats.impressiveness = 0;   break; // Awful
                case 1:  stats.impressiveness = 15;  break; // Dull
                case 2:  stats.impressiveness = 30;  break; // Mediocre
                case 3:  stats.impressiveness = 45;  break; // Decent
                case 4:  stats.impressiveness = 60;  break; // Slightly Impressive
                case 5:  stats.impressiveness = 75;  break; // Somewhat Impressive
                case 6:  stats.impressiveness = 90;  break; // Impressive
                case 7:  stats.impressiveness = 105; break; // Very Impressive
                case 8:  stats.impressiveness = 120; break; // Extremely Impressive
                case 9:  stats.impressiveness = 150; break; // Unbelievably Impressive
                default: stats.impressiveness = 30;  break;
            }
            
            // Calculate quality from bedding upgrades
            // Each increasesBedQuality upgrade adds one quality tier (25 points)
            // Hospitality formula: (QualityCategory - 2) * 25, so Poor = 0, Normal = 25, etc.
            int qualityUpgradeCount = 0;
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.increasesBedQuality)
                {
                    qualityUpgradeCount++;
                }
            }
            stats.quality = qualityUpgradeCount * 25;
            
            // Calculate room type score based on floor type
            // Barracks = 0, Single bedroom = 90 (30 base + 60 single bed bonus), Multiple rooms = 30
            if (IsBarracks)
            {
                stats.roomTypeScore = 0;
            }
            else if (BedCount >= 4)
            {
                // Multiple private rooms - treated as a guest room
                stats.roomTypeScore = 30;
            }
            else
            {
                // Single bedroom (1-2 beds = one double bed room)
                // 30 (GuestRoom base) + 60 (single room bonus) = 90
                stats.roomTypeScore = 90;
            }
            
            // Calculate comfort from bed stats
            // Hospitality uses: (int)(100 * bed.GetStatValue(Comfort))
            if (parent is Building_Bed bed)
            {
                stats.comfort = (int)(100 * bed.GetStatValue(RimWorld.StatDefOf.Comfort));
            }
            else
            {
                stats.comfort = 50; // Default fallback
            }
            
            // Calculate facilities from upgrades with countsAsFacility
            // Each facility adds 10 points
            int facilityCount = 0;
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.countsAsFacility)
                {
                    facilityCount++;
                }
            }
            stats.facilities = facilityCount * 10;
            
            return stats;
        }
        
        /// <summary>
        /// Gets the reason why an upgrade is disabled, or None if it's active.
        /// </summary>
        public UpgradeDisableReason GetUpgradeDisableReason(StaircaseUpgradeDef def)
        {
            if (!HasUpgrade(def))
            {
                return UpgradeDisableReason.None; // Not constructed at all
            }

            var constructedUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (constructedUpgrade == null)
            {
                return UpgradeDisableReason.None;
            }

            // Check if manually toggled off first (takes priority)
            if (constructedUpgrade.isToggledOff && def.CanBeToggled)
            {
                return UpgradeDisableReason.ToggledOff;
            }

            // Check if upgrade requires power
            if (def.requiresPower)
            {
                if (!HasPower())
                {
                    return UpgradeDisableReason.NoPower;
                }
            }

            // Check if upgrade requires fuel
            if (def.fuelPerBed > 0f)
            {
                var refuelable = parent.GetComp<CompRefuelable>();
                if (refuelable != null && !refuelable.HasFuel)
                {
                    return UpgradeDisableReason.OutOfFuel;
                }
            }

            // Check if this upgrade requires one per bed
            if (def.upgradeBuildingDef != null)
            {
                var ext = def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.onePerBed == true)
                {
                    int bedCount = BedCount;
                    
                    // For barracks with ambient upgrades (not directly to bed), halve the required count
                    bool directlyToBed = ext?.directlyToBed ?? false;
                    if (!directlyToBed && IsBarracks && bedCount > 1)
                    {
                        bedCount = Mathf.CeilToInt(bedCount / 2f);
                    }
                    
                    if (constructedUpgrade.count < bedCount)
                    {
                        return UpgradeDisableReason.InsufficientCount;
                    }
                }
            }

            // Check if controllable fueled temp changer is on standby (target temp reached)
            if (def.followsDesiredTemp && def.fuelPerBed > 0f && def.heatOffset > 0f)
            {
                float utilizationRatio = GetFueledUtilizationRatio();
                if (utilizationRatio <= 0f)
                {
                    return UpgradeDisableReason.ReachedTemperature;
                }
            }

            return UpgradeDisableReason.None;
        }
        
        /// <summary>
        /// Checks if an upgrade is basically active (not toggled off, has power/fuel, sufficient count).
        /// This does NOT check ReachedTemperature status to avoid circular dependencies.
        /// Used internally for temperature calculations.
        /// </summary>
        private bool IsUpgradeBasicallyActive(StaircaseUpgradeDef def)
        {
            if (!HasUpgrade(def))
                return false;

            var constructedUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (constructedUpgrade == null)
                return false;

            // Check if manually toggled off
            if (constructedUpgrade.isToggledOff && def.CanBeToggled)
                return false;

            // Check if upgrade requires power
            if (def.requiresPower && !HasPower())
                return false;

            // Check if upgrade requires fuel
            if (def.fuelPerBed > 0f)
            {
                var refuelable = parent.GetComp<CompRefuelable>();
                if (refuelable != null && !refuelable.HasFuel)
                    return false;
            }

            // Check if this upgrade requires one per bed
            if (def.upgradeBuildingDef != null)
            {
                var ext = def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>();
                if (ext?.onePerBed == true)
                {
                    int bedCount = BedCount;
                    
                    bool directlyToBed = ext?.directlyToBed ?? false;
                    if (!directlyToBed && IsBarracks && bedCount > 1)
                    {
                        bedCount = Mathf.CeilToInt(bedCount / 2f);
                    }
                    
                    if (constructedUpgrade.count < bedCount)
                        return false;
                }
            }

            return true;
        }
        
        /// <summary>
        /// Returns true if the staircase has power.
        /// </summary>
        public bool HasPower()
        {
            var powerComp = parent.GetComp<CompPowerTrader>();
            if (powerComp == null)
            {
                // If no power comp exists, assume powered (for backwards compatibility)
                return true;
            }
            return powerComp.PowerOn;
        }
        
        // =====================================================
        // Linked Battery Methods
        // =====================================================
        
        /// <summary>
        /// Returns true if any constructed upgrade spawns a linked battery.
        /// </summary>
        public bool HasBatteryStorage()
        {
            return constructedUpgrades.Any(au => au.def.IsBatteryUpgrade);
        }
        
        /// <summary>
        /// Gets the linked battery building, if any.
        /// </summary>
        public Thing LinkedBattery => linkedBattery;
        
        /// <summary>
        /// Gets the battery comp from the linked battery building.
        /// </summary>
        public CompPowerBattery LinkedBatteryComp
        {
            get
            {
                if (linkedBattery == null || linkedBattery.Destroyed)
                    return null;
                return linkedBattery.TryGetComp<CompPowerBattery>();
            }
        }
        
        /// <summary>
        /// Gets the current stored energy from the linked battery.
        /// </summary>
        public float StoredEnergy => LinkedBatteryComp?.StoredEnergy ?? 0f;
        
        /// <summary>
        /// Gets the stored energy as a percentage of max capacity.
        /// </summary>
        public float StoredEnergyPct => LinkedBatteryComp?.StoredEnergyPct ?? 0f;
        
        /// <summary>
        /// Gets the total battery capacity from the linked battery's props.
        /// </summary>
        public float GetTotalBatteryCapacity()
        {
            return LinkedBatteryComp?.Props?.storedEnergyMax ?? 0f;
        }
        
        /// <summary>
        /// Gets the battery efficiency from the linked battery's props.
        /// </summary>
        public float GetBatteryEfficiency()
        {
            return LinkedBatteryComp?.Props?.efficiency ?? 0.5f;
        }
        
        /// <summary>
        /// Spawns the linked battery building for a battery upgrade.
        /// </summary>
        private void SpawnLinkedBattery(StaircaseUpgradeDef upgradeDef)
        {
            if (upgradeDef.linkedBatteryDef == null)
                return;
                
            // Don't spawn if already have a linked battery
            if (linkedBattery != null && !linkedBattery.Destroyed && linkedBattery.Spawned)
                return;
            
            Thing battery = ThingMaker.MakeThing(upgradeDef.linkedBatteryDef);
            GenSpawn.Spawn(battery, parent.Position, parent.Map);
            
            // Link the battery to this staircase
            if (battery is Building_StaircaseBattery staircaseBattery)
            {
                staircaseBattery.parentStaircase = parent;
            }
            
            linkedBattery = battery;
        }
        
        /// <summary>
        /// Destroys the linked battery building if it exists.
        /// </summary>
        private void DestroyLinkedBattery()
        {
            if (linkedBattery != null && !linkedBattery.Destroyed)
            {
                linkedBattery.Destroy(DestroyMode.Vanish);
            }
            linkedBattery = null;
        }
        
        // =====================================================
        // Linked Bathroom System (DBH Integration)
        // =====================================================
        
        /// <summary>
        /// Gets the linked bathroom building.
        /// </summary>
        public Thing LinkedBathroom => linkedBathroom;
        
        /// <summary>
        /// Spawns the linked bathroom building for a bathroom upgrade.
        /// </summary>
        private void SpawnLinkedBathroom(StaircaseUpgradeDef upgradeDef)
        {
            if (upgradeDef.linkedBathroomDef == null)
                return;
                
            // Don't spawn if already have a linked bathroom
            if (linkedBathroom != null && !linkedBathroom.Destroyed && linkedBathroom.Spawned)
                return;
            
            Thing bathroom = ThingMaker.MakeThing(upgradeDef.linkedBathroomDef);
            GenSpawn.Spawn(bathroom, parent.Position, parent.Map);
            
            // Link the bathroom to this staircase
            if (bathroom is Building_StaircaseBathroom staircaseBathroom)
            {
                staircaseBathroom.parentStaircase = parent;
            }
            
            linkedBathroom = bathroom;
        }
        
        /// <summary>
        /// Destroys the linked bathroom building if it exists.
        /// </summary>
        private void DestroyLinkedBathroom()
        {
            if (linkedBathroom != null && !linkedBathroom.Destroyed)
            {
                linkedBathroom.Destroy(DestroyMode.Vanish);
            }
            linkedBathroom = null;
        }
        
        /// <summary>
        /// Returns true if any constructed upgrade is a bathroom upgrade.
        /// </summary>
        public bool HasAnyBathroomUpgrade()
        {
            return constructedUpgrades.Any(au => au.def.IsBathroomUpgrade);
        }
        
        /// <summary>
        /// Returns true if any constructed upgrade requires power.
        /// </summary>
        public bool HasAnyPowerRequiringUpgrade()
        {
            return constructedUpgrades.Any(au => au.def.requiresPower);
        }
        
        /// <summary>
        /// Returns true if any constructed upgrade requires fuel.
        /// </summary>
        public bool HasAnyFuelRequiringUpgrade()
        {
            return constructedUpgrades.Any(au => au.def.fuelPerBed > 0f);
        }
        
        /// <summary>
        /// Returns true if the parent has fuel available.
        /// </summary>
        public bool HasFuel()
        {
            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable == null)
                return true;
            return refuelable.HasFuel;
        }
        
        /// <summary>
        /// Returns true if any constructed upgrade is a smart temperature modifier.
        /// </summary>
        public bool HasAnySmartTempModifier()
        {
            return constructedUpgrades.Any(au => au.def.IsSmartTempModifier);
        }

        public bool HasAnyDumbTempModifier()
        {
            return constructedUpgrades.Any(au => au.def.IsDumbTempModifier);
        }

        public bool HasAnyInsulatingModifier()
        {
            return constructedUpgrades.Any(au => au.def.insulationAdjustment > 0f);
        }
        
        /// <summary>
        /// Returns true if any constructed upgrade is a controllable fueled temp changer (followsDesiredTemp = true).
        /// </summary>
        public bool HasAnyControllableFueledTempChanger()
        {
            return constructedUpgrades.Any(au => au.def.followsDesiredTemp && au.def.fuelPerBed > 0f);
        }
        
        /// <summary>
        /// Returns true if both controllable fueled temp changers AND smart (electric) temp changers are installed.
        /// Used to determine if the priority toggle should be shown.
        /// </summary>
        public bool HasControllableFueledAndSmartTempChangers
        {
            get
            {
                return HasAnyControllableFueledTempChanger() && HasAnySmartTempModifier();
            }
        }
        
        /// <summary>
        /// Returns true if the specified upgrade is toggled off.
        /// </summary>
        public bool IsUpgradeToggledOff(StaircaseUpgradeDef def)
        {
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            return activeUpgrade?.isToggledOff ?? false;
        }
        
        /// <summary>
        /// Sets the toggle state for an upgrade. Only works for upgrades that can be toggled.
        /// </summary>
        public void SetUpgradeToggled(StaircaseUpgradeDef def, bool toggledOff)
        {
            if (!def.CanBeToggled)
            {
                return;
            }
            
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade != null)
            {
                activeUpgrade.isToggledOff = toggledOff;
            }
        }
        
        /// <summary>
        /// Toggles an upgrade's on/off state. Only works for upgrades that can be toggled.
        /// </summary>
        public void ToggleUpgrade(StaircaseUpgradeDef def)
        {
            if (!def.CanBeToggled)
            {
                return;
            }
            
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade != null)
            {
                activeUpgrade.isToggledOff = !activeUpgrade.isToggledOff;
            }
        }

        /// <summary>
        /// Helper method to get all active upgrade defs (valid upgrades only).
        /// For upgrades with onePerBed=true, they must have count >= bedCount to be valid.
        /// Used for all non-bed-count effects (thoughts, temperature, etc.).
        /// </summary>
        public List<StaircaseUpgradeDef> GetActiveUpgradeDefs()
        {

            if (constructedUpgrades == null || constructedUpgrades.Count == 0)
                return new List<StaircaseUpgradeDef>();

            List<StaircaseUpgradeDef> activeDefs = new List<StaircaseUpgradeDef>();
            
            foreach (var constructedUpgrade in constructedUpgrades)
            {
                // Use the new GetUpgradeDisableReason method to determine if upgrade is active
                if (GetUpgradeDisableReason(constructedUpgrade.def) == UpgradeDisableReason.None)
                {
                    activeDefs.Add(constructedUpgrade.def);
                }
            }
            
            return activeDefs;
        }
        
        /// <summary>
        /// Adds an upgrade with optional stuff
        /// </summary>
        public void AddUpgrade(StaircaseUpgradeDef def, ThingDef stuff)
        {
            if (!HasUpgrade(def))
            {
                constructedUpgrades.Add(new ActiveUpgrade(def, stuff));
                
                // Invalidate bed count cache since upgrades may modify bed count
                InvalidateBedCountCache();
                
                // Trigger Hospitality to recalculate guest bed stats
                if (parent is Building_Bed bed)
                {
                    HospitalityPatches.TriggerGuestBedStatsUpdate(bed);
                }
                
                // If this is a battery upgrade, spawn the linked battery building
                if (def.IsBatteryUpgrade && parent.Spawned)
                {
                    SpawnLinkedBattery(def);
                }
                
                // If this is a bathroom upgrade, spawn the linked bathroom building
                if (def.linkedBathroomDef != null && parent.Spawned)
                {
                    SpawnLinkedBathroom(def);
                }
            }
        }

        /// <summary>
        /// Increases the count of an existing upgrade
        /// </summary>
        public void IncreaseUpgradeCount(StaircaseUpgradeDef def)
        {
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade != null)
            {
                activeUpgrade.count++;
                InvalidateBedCountCache();
                
                // Trigger Hospitality to recalculate guest bed stats
                if (parent is Building_Bed bed)
                {
                    HospitalityPatches.TriggerGuestBedStatsUpdate(bed);
                }
            }
        }
        
        /// <summary>
        /// Gets the constructed count for a specific upgrade.
        /// Returns 0 if the upgrade is not constructed.
        /// </summary>
        public int GetConstructedCount(StaircaseUpgradeDef def)
        {
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            return activeUpgrade?.count ?? 0;
        }
        
        /// <summary>
        /// Removes an upgrade
        /// </summary>
        public void RemoveUpgrade(StaircaseUpgradeDef def)
        {
            constructedUpgrades.RemoveAll(au => au.def == def);
            InvalidateBedCountCache();
            
            // Trigger Hospitality to recalculate guest bed stats
            if (parent is Building_Bed bed)
            {
                HospitalityPatches.TriggerGuestBedStatsUpdate(bed);
            }
        }
        
        /// <summary>
        /// Decreases the count of an existing upgrade by one.
        /// Returns true if successfully decreased, false if the upgrade doesn't exist or count is already 0.
        /// </summary>
        public bool DecreaseUpgradeCount(StaircaseUpgradeDef def)
        {
            var activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade == null || activeUpgrade.count <= 0)
            {
                return false;
            }
            
            activeUpgrade.count--;
            InvalidateBedCountCache();
            
            // If count reaches 0, remove the upgrade entirely
            if (activeUpgrade.count <= 0)
            {
                constructedUpgrades.Remove(activeUpgrade);
            }
            
            // Trigger Hospitality to recalculate guest bed stats
            if (parent is Building_Bed bed)
            {
                HospitalityPatches.TriggerGuestBedStatsUpdate(bed);
            }
            
            return true;
        }
        
        /// <summary>
        /// Removes excess instances of an upgrade and refunds materials.
        /// Returns the number of instances removed and the refund info.
        /// </summary>
        public (int removed, string refundInfo) RemoveExcessUpgradesWithRefund(StaircaseUpgradeDef def, int excessCount, float refundPercent = 0.75f)
        {
            ActiveUpgrade activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade == null || excessCount <= 0)
            {
                return (0, null);
            }
            
            int actualRemoved = Mathf.Min(excessCount, activeUpgrade.count);
            ThingDef stuff = activeUpgrade.stuff;
            string refundInfo = "";
            
            // Calculate refund if there's a cost and stuff was used
            if (stuff != null)
            {
                int baseCost = 0;
                if (def.RequiresConstruction && def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
                {
                    baseCost = def.upgradeBuildingDef.costStuffCount;
                }
                
                if (baseCost > 0)
                {
                    int totalCost = baseCost * actualRemoved;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);
                    
                    if (refundAmount > 0)
                    {
                        Thing refundThing = ThingMaker.MakeThing(stuff);
                        refundThing.stackCount = refundAmount;
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);
                        refundInfo = $" ({refundAmount} {stuff.label} refunded)";
                    }
                }
            }
            
            // Handle costList items
            if (def.upgradeBuildingDef?.costList != null)
            {
                foreach (var cost in def.upgradeBuildingDef.costList)
                {
                    int totalCost = cost.count * actualRemoved;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);
                    
                    if (refundAmount > 0)
                    {
                        Thing refundThing = ThingMaker.MakeThing(cost.thingDef);
                        refundThing.stackCount = refundAmount;
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);
                        
                        if (!string.IsNullOrEmpty(refundInfo))
                        {
                            refundInfo += ", ";
                        }
                        else
                        {
                            refundInfo = " (";
                        }
                        refundInfo += $"{refundAmount} {cost.thingDef.label}";
                    }
                }
                
                if (!string.IsNullOrEmpty(refundInfo) && !refundInfo.EndsWith(")"))
                {
                    refundInfo += " refunded)";
                }
            }
            
            // Decrease the count
            activeUpgrade.count -= actualRemoved;
            
            // If count reaches 0, remove the upgrade entirely
            if (activeUpgrade.count <= 0)
            {
                constructedUpgrades.Remove(activeUpgrade);
            }
            
            // Invalidate bed count cache since upgrades may modify bed count
            InvalidateBedCountCache();
            
            return (actualRemoved, refundInfo);
        }
        
        /// <summary>
        /// Removes all constructed instances of an upgrade and refunds materials.
        /// Returns the refunded material information for messaging.
        /// </summary>
        public string RemoveConstructedUpgradesWithRefund(StaircaseUpgradeDef def, float refundPercent = 0.75f)
        {
            // Find the constructed upgrade to get the stuff used
            ActiveUpgrade activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade == null || activeUpgrade.count == 0)
            {
                return null; // Upgrade not found or no constructed instances
            }

            int constructedCount = activeUpgrade.count;
            ThingDef stuff = activeUpgrade.stuff;
            string refundInfo = "";

            // Calculate refund if there's a cost and stuff was used
            if (stuff != null)
            {
                // Get base cost from costStuffCount
                int baseCost = 0;
                if (def.RequiresConstruction && def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
                {
                    baseCost = def.upgradeBuildingDef.costStuffCount;
                }
                else if (def.upgradeBuildingDef?.costList != null && def.upgradeBuildingDef.costList.Count > 0)
                {
                    // Fallback to costList if available
                    baseCost = def.upgradeBuildingDef.costList[0].count;
                }

                if (baseCost > 0)
                {
                    int totalCost = baseCost * constructedCount;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);

                    if (refundAmount > 0)
                    {
                        // Create and spawn the refund
                        Thing refundThing = ThingMaker.MakeThing(stuff);
                        refundThing.stackCount = refundAmount;

                        // Try to spawn near the staircase
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);

                        refundInfo = $" ({refundAmount} {stuff.label} refunded)";
                    }
                }
            }

            // Handle costList items
            if (def.upgradeBuildingDef?.costList != null)
            {
                foreach (var cost in def.upgradeBuildingDef.costList)
                {
                    int totalCost = cost.count * constructedCount;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);

                    if (refundAmount > 0)
                    {
                        // Create and spawn the refund
                        Thing refundThing = ThingMaker.MakeThing(cost.thingDef);
                        refundThing.stackCount = refundAmount;

                        // Try to spawn near the staircase
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);

                        if (!string.IsNullOrEmpty(refundInfo))
                        {
                            refundInfo += ", ";
                        }
                        else
                        {
                            refundInfo = " (";
                        }
                        refundInfo += $"{refundAmount} {cost.thingDef.label}";
                    }
                }
                
                if (!string.IsNullOrEmpty(refundInfo) && !refundInfo.EndsWith(")"))
                {
                    refundInfo += " refunded)";
                }
            }

            // Remove the upgrade completely
            constructedUpgrades.RemoveAll(au => au.def == def);

            // Invalidate bed count cache since upgrades may modify bed count
            InvalidateBedCountCache();

            return refundInfo;
        }
        
        /// <summary>
        /// Removes an upgrade and refunds materials based on the stuff used and bed count.
        /// Returns the refunded material information for messaging.
        /// </summary>
        public string RemoveUpgradeWithRefund(StaircaseUpgradeDef def, float refundPercent = 0.75f)
        {
            // Find the constructed upgrade to get the stuff used
            ActiveUpgrade activeUpgrade = constructedUpgrades.FirstOrDefault(au => au.def == def);
            if (activeUpgrade == null)
            {
                return null; // Upgrade not found
            }

            int bedCount = GetBedCount(def);

            ThingDef stuff = activeUpgrade.stuff;
            string refundInfo = "";

            // Calculate refund if there's a cost and stuff was used
            if (stuff != null)
            {
                // Get base cost from costStuffCount
                int baseCost = 0;
                if (def.RequiresConstruction && def.upgradeBuildingDef != null && def.upgradeBuildingDef.costStuffCount > 0)
                {
                    baseCost = def.upgradeBuildingDef.costStuffCount;
                }
                else if (def.upgradeBuildingDef.costList != null && def.upgradeBuildingDef.costList.Count > 0)
                {
                    // Fallback to costList if available
                    baseCost = def.upgradeBuildingDef.costList[0].count;
                }

                if (baseCost > 0)
                {
                    int totalCost = baseCost * bedCount;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);

                    if (refundAmount > 0)
                    {
                        // Create and spawn the refund
                        Thing refundThing = ThingMaker.MakeThing(stuff);
                        refundThing.stackCount = refundAmount;

                        // Try to spawn near the staircase
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);

                        refundInfo = $" ({refundAmount} {stuff.label} refunded)";
                    }
                }
            }

            if (def.upgradeBuildingDef.costList != null)
            {
                foreach (var cost in def.upgradeBuildingDef.costList)
                {

                    int totalCost = cost.count;
                    totalCost *= bedCount;

                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);

                    if (refundAmount > 0)
                    {
                        // Create and spawn the refund
                        Thing refundThing = ThingMaker.MakeThing(cost.thingDef);
                        refundThing.stackCount = refundAmount;

                        // Try to spawn near the staircase
                        IntVec3 dropPos = parent.Position;
                        GenPlace.TryPlaceThing(refundThing, dropPos, parent.Map, ThingPlaceMode.Near);

                        refundInfo += $" ({refundAmount} {cost.thingDef.label} refunded)";
                    }
                }
            }

            // Remove the upgrade
            constructedUpgrades.RemoveAll(au => au.def == def);

            // Invalidate bed count cache since upgrades may modify bed count
            InvalidateBedCountCache();

            return refundInfo;
        }

        /// <summary>
        /// Helper method to get bed count for an upgrade. Sets to 1 if not applicable to the upgrade.
        /// For onePerBed upgrades that are not directlyToBed, the count is halved in barracks
        /// (rounded up) since barracks share ambient upgrades between beds.
        /// </summary>
        private int GetBedCount(StaircaseUpgradeDef def)
        {
            // Get bed count to calculate total cost if applicable
            int bedCount = 1;
            var ext = def.upgradeBuildingDef?.GetModExtension<StaircaseUpgradeExtension>();
            bool onePerBed = ext?.onePerBed ?? true;
            if (onePerBed)
            {
                bedCount = BedCount;
                
                // For barracks with ambient upgrades (not directly to bed), halve the required count
                bool directlyToBed = ext?.directlyToBed ?? false;
                if (!directlyToBed && IsBarracks && bedCount > 1)
                {
                    bedCount = Mathf.CeilToInt(bedCount / 2f);
                }
            }

            return bedCount;
        }

        /// <summary>
        /// Gets the required bed count for UI display purposes.
        /// This is separate from GetBedCount since it may show different values for player clarity.
        /// </summary>
        public int GetRequiredBedCountForUpgrade(StaircaseUpgradeDef def)
        {
            return GetBedCount(def);
        }

        /// <summary>
        /// Gets the virtual temperature for the Second Floor.
        /// By default, returns outdoor (map ambient) temperature.
        /// Upgrades can modify this temperature in the future.
        /// </summary>
        public float CurrentVirtualTemperature
        {
            get
            {
                return CalculateVirtualTemperature();
            }
        }
        
        /// <summary>
        /// Gets the current total power consumption for display purposes.
        /// </summary>
        public float CurrentPowerConsumption => cachedTotalPowerConsumption;
        
        /// <summary>
        /// Gets the current fuel consumption rate (per day) for display purposes.
        /// </summary>
        public float CurrentFuelConsumption => cachedFuelConsumptionRate;
        
        public float GetInsulatedTemperature()
        {
            if (parent?.Map == null) return 21f;
            
            float temp = parent.Map.mapTemperature.OutdoorTemp;
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs()
                .Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            
            float totalInsulation = activeUpgrades.Sum(au => au.def.insulationAdjustment);
            if (totalInsulation > 0)
            {
                float weightedTargetSum = 0f;
                float weightSum = 0f;
                foreach (var activeUpgrade in activeUpgrades)
                {
                    if (activeUpgrade.def.insulationAdjustment > 0)
                    {
                        weightedTargetSum += activeUpgrade.def.insulationTarget * activeUpgrade.def.insulationAdjustment;
                        weightSum += activeUpgrade.def.insulationAdjustment;
                    }
                }
                float insulationTarget = weightSum > 0 ? weightedTargetSum / weightSum : 21f;
                float diff = insulationTarget - temp;
                float correction = Mathf.Clamp(diff, -totalInsulation, totalInsulation);
                temp += correction;
            }
            
            return temp;
        }

        /// <summary>
        /// Gets the base temperature before smart temp modifiers are applied.
        /// This includes outdoor temp, insulation, and dumb heaters/coolers.
        /// </summary>
        public float GetBaseTemperature()
        {
            if (parent?.Map == null) return 21f;
            
            float temp = parent.Map.mapTemperature.OutdoorTemp;
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs()
                .Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            
            // Step 1: Apply Insulation
            float totalInsulation = activeUpgrades.Sum(au => au.def.insulationAdjustment);
            if (totalInsulation > 0)
            {
                float weightedTargetSum = 0f;
                float weightSum = 0f;
                foreach (var activeUpgrade in activeUpgrades)
                {
                    if (activeUpgrade.def.insulationAdjustment > 0)
                    {
                        weightedTargetSum += activeUpgrade.def.insulationTarget * activeUpgrade.def.insulationAdjustment;
                        weightSum += activeUpgrade.def.insulationAdjustment;
                    }
                }
                float insulationTarget = weightSum > 0 ? weightedTargetSum / weightSum : 21f;
                float diff = insulationTarget - temp;
                float correction = Mathf.Clamp(diff, -totalInsulation, totalInsulation);
                temp += correction;
            }
            
            // Step 2: Apply Dumb Heaters (clamped to their max caps)
            var dumbHeaters = activeUpgrades.Where(au => au.def.IsDumbTempModifier && au.def.heatOffset > 0).ToList();
            foreach (var heater in dumbHeaters)
            {
                // Calculate clamped heat offset
                float potentialTemp = temp + heater.def.heatOffset;
                float actualHeat = heater.def.heatOffset;
                
                // Clamp: heater cannot push temp above its maxHeatCap
                if (potentialTemp > heater.def.maxHeatCap)
                {
                    actualHeat = Mathf.Max(0f, heater.def.maxHeatCap - temp);
                }
                
                temp += actualHeat;
            }
            
            // Step 3: Apply Dumb Coolers (clamped to their min caps)
            var dumbCoolers = activeUpgrades.Where(au => au.def.IsDumbTempModifier && au.def.coolOffset > 0).ToList();
            foreach (var cooler in dumbCoolers)
            {
                // Calculate clamped cool offset
                float potentialTemp = temp - cooler.def.coolOffset;
                float actualCool = cooler.def.coolOffset;
                
                // Clamp: cooler cannot push temp below its minCoolCap
                if (potentialTemp < cooler.def.minCoolCap)
                {
                    actualCool = Mathf.Max(0f, temp - cooler.def.minCoolCap);
                }
                
                temp -= actualCool;
            }
            
            return temp;
        }

        /// <summary>
        /// Gets the temperature after applying outdoor temp, insulation, and uncontrollable dumb temp changers.
        /// This is the base for calculating how much controllable fueled changers need to work.
        /// Uses IsUpgradeBasicallyActive to avoid circular dependency with GetFueledUtilizationRatio.
        /// </summary>
        public float GetPreControllableTemperature()
        {
            if (parent?.Map == null) return 21f;
            
            float temp = parent.Map.mapTemperature.OutdoorTemp;
            
            // Get basically active upgrades (avoids circular dependency)
            var basicActiveUpgrades = constructedUpgrades
                .Where(au => IsUpgradeBasicallyActive(au.def)).ToList();
            
            // Step 1: Apply Insulation
            float totalInsulation = basicActiveUpgrades.Sum(au => au.def.insulationAdjustment);
            if (totalInsulation > 0)
            {
                float weightedTargetSum = 0f;
                float weightSum = 0f;
                foreach (var activeUpgrade in basicActiveUpgrades)
                {
                    if (activeUpgrade.def.insulationAdjustment > 0)
                    {
                        weightedTargetSum += activeUpgrade.def.insulationTarget * activeUpgrade.def.insulationAdjustment;
                        weightSum += activeUpgrade.def.insulationAdjustment;
                    }
                }
                float insulationTarget = weightSum > 0 ? weightedTargetSum / weightSum : 21f;
                float diff = insulationTarget - temp;
                float correction = Mathf.Clamp(diff, -totalInsulation, totalInsulation);
                temp += correction;
            }
            
            // Step 2: Apply Uncontrollable Dumb Heaters (those with followsDesiredTemp = false)
            var uncontrollableHeaters = basicActiveUpgrades.Where(au => 
                au.def.IsDumbTempModifier && au.def.heatOffset > 0 && !au.def.followsDesiredTemp).ToList();
            foreach (var heater in uncontrollableHeaters)
            {
                float potentialTemp = temp + heater.def.heatOffset;
                float actualHeat = heater.def.heatOffset;
                if (potentialTemp > heater.def.maxHeatCap)
                {
                    actualHeat = Mathf.Max(0f, heater.def.maxHeatCap - temp);
                }
                temp += actualHeat;
            }
            
            // Step 3: Apply Uncontrollable Dumb Coolers (those with followsDesiredTemp = false)
            var uncontrollableCoolers = basicActiveUpgrades.Where(au => 
                au.def.IsDumbTempModifier && au.def.coolOffset > 0 && !au.def.followsDesiredTemp).ToList();
            foreach (var cooler in uncontrollableCoolers)
            {
                float potentialTemp = temp - cooler.def.coolOffset;
                float actualCool = cooler.def.coolOffset;
                if (potentialTemp < cooler.def.minCoolCap)
                {
                    actualCool = Mathf.Max(0f, temp - cooler.def.minCoolCap);
                }
                temp -= actualCool;
            }
            
            return temp;
        }

        /// <summary>
        /// Calculates the utilization ratio (0.0 to 1.0) for controllable fueled temperature changers.
        /// Returns how much they need to work to reach the target temperature.
        /// 0.0 = not needed (temperature already achieved), 1.0 = full capacity needed.
        /// Uses IsUpgradeBasicallyActive to avoid circular dependency with GetUpgradeDisableReason.
        /// Respects the priority toggle - if electric goes first, fueled calculates based on temp after electric.
        /// </summary>
        public float GetFueledUtilizationRatio()
        {
            if (parent?.Map == null) return 0f;
            
            // Get temperature before controllable changers are applied
            float baseTemp = GetPreControllableTemperature();
            
            // If electric goes first, we need to calculate temp after smart modifiers
            if (!preferFueledFirst && HasPower())
            {
                baseTemp = GetTempAfterSmartModifiers(baseTemp);
            }
            
            // Get basically active controllable fueled heaters (avoids circular dependency)
            var controllableHeaters = constructedUpgrades
                .Where(au => IsUpgradeBasicallyActive(au.def) && 
                             au.def.IsDumbTempModifier && au.def.heatOffset > 0 && au.def.followsDesiredTemp)
                .ToList();
            
            if (!controllableHeaters.Any())
                return 0f;
            
            float totalHeatingCapacity = controllableHeaters.Sum(h => h.def.heatOffset);
            if (totalHeatingCapacity <= 0f)
                return 0f;
            
            // Calculate how much heating is needed to reach target
            float tempDiff = targetTemperature - baseTemp;
            
            // If we don't need heating (already at or above target), return 0
            if (tempDiff <= 0f)
                return 0f;
            
            // Calculate ratio: how much of max capacity is needed
            float ratio = Mathf.Clamp01(tempDiff / totalHeatingCapacity);
            
            return ratio;
        }
        
        /// <summary>
        /// Calculates the utilization ratio (0.0 to 1.0) for smart (electric) temperature changers.
        /// Returns how much they need to work to reach the target temperature.
        /// 0.0 = not needed (temperature already achieved), 1.0 = full capacity needed.
        /// Respects the priority toggle - if fueled goes first, smart calculates based on temp after fueled.
        /// </summary>
        public float GetSmartUtilizationRatio(StaircaseUpgradeDef def = null)
        {
            if (parent?.Map == null || !HasPower()) return 0f;
            
            // Get temperature before controllable changers are applied
            float baseTemp = GetPreControllableTemperature();
            
            // If fueled goes first, we need to calculate temp after fueled heaters
            if (preferFueledFirst)
            {
                baseTemp = GetTempAfterFueledHeaters(baseTemp);
            }
            
            float tempDiff = targetTemperature - baseTemp;
            
            // If at target, minimal power for standby
            if (Mathf.Abs(tempDiff) < 0.5f)
                return 0.05f;
            
            // Get smart modifiers
            var smartModifiers = constructedUpgrades
                .Where(au => IsUpgradeBasicallyActive(au.def) && au.def.IsSmartTempModifier)
                .ToList();
            
            if (!smartModifiers.Any())
                return 0f;
            
            // If specific def provided, check if it can address the current need
            if (def != null)
            {
                bool needsHeating = tempDiff > 0f;
                bool needsCooling = tempDiff < 0f;
                bool canHeat = def.smartTempModifierType == TempModifierType.HeaterOnly || 
                               def.smartTempModifierType == TempModifierType.DualMode;
                bool canCool = def.smartTempModifierType == TempModifierType.CoolerOnly || 
                               def.smartTempModifierType == TempModifierType.DualMode;
                
                if (needsHeating && !canHeat) return 0f;
                if (needsCooling && !canCool) return 0f;
            }
            
            // Calculate total capacity in the relevant direction
            float totalCapacity = 0f;
            bool needsHeatingGeneral = tempDiff > 0f;
            
            foreach (var mod in smartModifiers)
            {
                if (needsHeatingGeneral)
                {
                    if (mod.def.smartTempModifierType == TempModifierType.HeaterOnly || 
                        mod.def.smartTempModifierType == TempModifierType.DualMode)
                    {
                        totalCapacity += mod.def.smartHeatEfficiency * (mod.def.basePowerConsumption / 100f);
                    }
                }
                else
                {
                    if (mod.def.smartTempModifierType == TempModifierType.CoolerOnly || 
                        mod.def.smartTempModifierType == TempModifierType.DualMode)
                    {
                        totalCapacity += mod.def.smartCoolEfficiency * (mod.def.basePowerConsumption / 100f);
                    }
                }
            }
            
            if (totalCapacity <= 0f)
                return 0f;
            
            // Calculate ratio based on how much of capacity is needed
            float ratio = Mathf.Clamp01(Mathf.Abs(tempDiff) / totalCapacity);
            
            // Minimum 10% when active to prevent rapid cycling
            ratio = Mathf.Max(0.1f, ratio);
            
            return ratio;
        }
        
        /// <summary>
        /// Calculates temperature after applying smart (electric) modifiers toward target.
        /// Used when electric has priority over fueled.
        /// </summary>
        private float GetTempAfterSmartModifiers(float startTemp)
        {
            if (!HasPower()) return startTemp;
            
            var smartModifiers = constructedUpgrades
                .Where(au => IsUpgradeBasicallyActive(au.def) && au.def.IsSmartTempModifier)
                .ToList();
            
            if (!smartModifiers.Any())
                return startTemp;
            
            float temp = startTemp;
            float tempDiff = targetTemperature - temp;
            
            if (Mathf.Abs(tempDiff) < 0.1f)
                return temp;
            
            // Calculate total capacity
            float totalHeatingCapacity = 0f;
            float totalCoolingCapacity = 0f;
            
            foreach (var mod in smartModifiers)
            {
                if (mod.def.smartTempModifierType == TempModifierType.HeaterOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    totalHeatingCapacity += mod.def.smartHeatEfficiency * (mod.def.basePowerConsumption / 100f);
                }
                if (mod.def.smartTempModifierType == TempModifierType.CoolerOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    totalCoolingCapacity += mod.def.smartCoolEfficiency * (mod.def.basePowerConsumption / 100f);
                }
            }
            
            // Apply toward target
            if (tempDiff > 0f && totalHeatingCapacity > 0f)
            {
                temp += Mathf.Min(tempDiff, totalHeatingCapacity);
            }
            else if (tempDiff < 0f && totalCoolingCapacity > 0f)
            {
                temp -= Mathf.Min(-tempDiff, totalCoolingCapacity);
            }
            
            return temp;
        }
        
        /// <summary>
        /// Calculates temperature after applying controllable fueled heaters toward target.
        /// Used when fueled has priority over electric.
        /// </summary>
        private float GetTempAfterFueledHeaters(float startTemp)
        {
            var controllableHeaters = constructedUpgrades
                .Where(au => IsUpgradeBasicallyActive(au.def) && 
                             au.def.IsDumbTempModifier && au.def.heatOffset > 0 && au.def.followsDesiredTemp)
                .ToList();
            
            if (!controllableHeaters.Any())
                return startTemp;
            
            float temp = startTemp;
            float tempDiff = targetTemperature - temp;
            
            // Only heat if needed
            if (tempDiff <= 0f)
                return temp;
            
            // Calculate total capacity (clamped by maxHeatCap)
            float totalCapacity = 0f;
            foreach (var heater in controllableHeaters)
            {
                float maxHeat = heater.def.heatOffset;
                if (temp + maxHeat > heater.def.maxHeatCap)
                {
                    maxHeat = Mathf.Max(0f, heater.def.maxHeatCap - temp);
                }
                totalCapacity += maxHeat;
            }
            
            // Apply only what's needed
            float heatToAdd = Mathf.Min(tempDiff, totalCapacity);
            temp += heatToAdd;
            
            return temp;
        }

        /// <summary>
        /// Calculates the virtual temperature for the Second Floor based on active upgrades.
        /// Applies insulation, uncontrollable dumb changers, then controllable fueled and smart modifiers
        /// based on the preferFueledFirst toggle.
        /// </summary>
        /// <returns>The calculated virtual temperature</returns>
        public float CalculateVirtualTemperature()
        {
            if (parent?.Map == null) return 21f; // Default temperature if not spawned

            // Get base temperature (outdoor + insulation + uncontrollable dumb heaters/coolers)
            float temp = GetPreControllableTemperature();
            
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs()
                .Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            
            // Get controllable fueled heaters
            var controllableHeaters = activeUpgrades.Where(au => 
                au.def.IsDumbTempModifier && au.def.heatOffset > 0 && au.def.followsDesiredTemp).ToList();
            
            // Get smart temp modifiers
            var smartTempModifiers = activeUpgrades.Where(au => au.def.IsSmartTempModifier).ToList();
            bool hasPower = HasPower();
            
            // Apply temperature modifiers based on priority
            if (preferFueledFirst)
            {
                // Apply controllable fueled heaters first (toward target temp)
                temp = ApplyControllableFueledHeaters(temp, controllableHeaters);
                
                // Then apply smart modifiers if still needed
                if (smartTempModifiers.Any() && hasPower)
                {
                    temp = ApplySmartTempModifiers(temp, smartTempModifiers);
                }
            }
            else
            {
                // Apply smart modifiers first
                if (smartTempModifiers.Any() && hasPower)
                {
                    temp = ApplySmartTempModifiers(temp, smartTempModifiers);
                }
                
                // Then apply controllable fueled heaters if still needed
                temp = ApplyControllableFueledHeaters(temp, controllableHeaters);
            }

            return temp;
        }
        
        /// <summary>
        /// Applies controllable fueled heaters toward the target temperature.
        /// Unlike dumb heaters, these won't overshoot the target.
        /// </summary>
        private float ApplyControllableFueledHeaters(float temp, List<ActiveUpgrade> controllableHeaters)
        {
            if (!controllableHeaters.Any())
                return temp;
            
            float tempDiff = targetTemperature - temp;
            
            // Only heat if we need to (temp is below target)
            if (tempDiff <= 0f)
                return temp;
            
            // Calculate total heating capacity
            float totalCapacity = 0f;
            foreach (var heater in controllableHeaters)
            {
                // Calculate clamped capacity (can't heat above maxHeatCap)
                float maxHeat = heater.def.heatOffset;
                if (temp + maxHeat > heater.def.maxHeatCap)
                {
                    maxHeat = Mathf.Max(0f, heater.def.maxHeatCap - temp);
                }
                totalCapacity += maxHeat;
            }
            
            // Apply only what's needed to reach target
            float heatToAdd = Mathf.Min(tempDiff, totalCapacity);
            temp += heatToAdd;
            
            return temp;
        }
        
        /// <summary>
        /// Applies smart (electric) temperature modifiers toward the target temperature.
        /// </summary>
        private float ApplySmartTempModifiers(float temp, List<ActiveUpgrade> smartTempModifiers)
        {
            float tempDiff = targetTemperature - temp;
            
            if (Mathf.Abs(tempDiff) < 0.1f)
            {
                return temp; // Already at target
            }
            
            // Calculate total heating and cooling capacity
            float totalHeatingCapacity = 0f;
            float totalCoolingCapacity = 0f;
            
            foreach (var mod in smartTempModifiers)
            {
                if (mod.def.smartTempModifierType == TempModifierType.HeaterOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    totalHeatingCapacity += mod.def.smartHeatEfficiency * (mod.def.basePowerConsumption / 100f);
                }
                
                if (mod.def.smartTempModifierType == TempModifierType.CoolerOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    totalCoolingCapacity += mod.def.smartCoolEfficiency * (mod.def.basePowerConsumption / 100f);
                }
            }
            
            // Apply heating or cooling based on need
            if (tempDiff > 0f && totalHeatingCapacity > 0f)
            {
                float heatToAdd = Mathf.Min(tempDiff, totalHeatingCapacity);
                temp += heatToAdd;
            }
            else if (tempDiff < 0f && totalCoolingCapacity > 0f)
            {
                float coolToAdd = Mathf.Min(-tempDiff, totalCoolingCapacity);
                temp -= coolToAdd;
            }
            
            return temp;
        }
        
        // =====================================================
        // Power Consumption Calculation
        // =====================================================
        
        /// <summary>
        /// Calculates the total power consumption for all power-requiring upgrades.
        /// Smart temperature modifiers have their power throttled based on temperature differential.
        /// </summary>
        /// <returns>Total power consumption in watts</returns>
        public float CalculateTotalPowerConsumption()
        {
            if (parent?.Map == null || constructedUpgrades == null || constructedUpgrades.Count == 0)
            {
                return 0f;
            }
            
            float totalPower = 0f;
            
            foreach (var activeUpgrade in constructedUpgrades)
            {
                if (!activeUpgrade.def.requiresPower)
                    continue;
                
                // Check if this upgrade is disabled for non-power reasons
                var disableReason = GetUpgradeDisableReason(activeUpgrade.def);
                if (disableReason != UpgradeDisableReason.None && disableReason != UpgradeDisableReason.NoPower)
                    continue;
                
                float upgradePower = activeUpgrade.def.basePowerConsumption * activeUpgrade.count;
                
                // Apply throttling for smart temperature modifiers
                if (activeUpgrade.def.IsSmartTempModifier)
                {
                    float throttle = GetSmartUtilizationRatio(activeUpgrade.def);
                    upgradePower *= throttle;
                }
                
                totalPower += upgradePower;
            }
            
            cachedTotalPowerConsumption = totalPower;
            return totalPower;
        }
        
        /// <summary>
        /// Calculates the throttle factor (0.0 to 1.0) for a smart temperature modifier.
        /// Based on the difference between base temperature and target temperature.
        /// </summary>
        /// <param name="def">The upgrade definition</param>
        /// <param name="baseTemp">The base temperature before smart modifiers</param>
        /// <returns>Throttle factor from 0.0 (off) to 1.0 (full power)</returns>
        private float CalculateSmartTempThrottle(StaircaseUpgradeDef def, float baseTemp)
        {
            float tempDiff = targetTemperature - baseTemp;
            
            // Determine if this modifier should be active based on temperature difference
            bool shouldHeat = tempDiff > 0f;
            bool shouldCool = tempDiff < 0f;
            
            bool canHeat = def.smartTempModifierType == TempModifierType.HeaterOnly || 
                          def.smartTempModifierType == TempModifierType.DualMode;
            bool canCool = def.smartTempModifierType == TempModifierType.CoolerOnly || 
                          def.smartTempModifierType == TempModifierType.DualMode;
            
            // If we need heating but can't heat, or need cooling but can't cool, throttle to 0
            if (shouldHeat && !canHeat)
                return 0f;
            if (shouldCool && !canCool)
                return 0f;
            
            // If we're at target temperature (within tolerance), minimal power
            if (Mathf.Abs(tempDiff) < 0.5f)
                return 0.05f; // 5% for standby/maintenance
            
            // Calculate throttle based on how far from target
            // Full power at 10°C difference or more, linear scale below that
            float absDiff = Mathf.Abs(tempDiff);
            float maxDiff = 10f; // Full power at 10°C difference
            
            float throttle = Mathf.Clamp01(absDiff / maxDiff);
            
            // Minimum 10% when active to prevent rapid on/off cycling
            throttle = Mathf.Max(0.1f, throttle);
            
            return throttle;
        }
        
        /// <summary>
        /// Gets detailed power breakdown for UI display.
        /// </summary>
        public string GetPowerBreakdownString()
        {
            if (!HasAnyPowerRequiringUpgrade())
                return null;
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float totalPower = 0f;
            
            foreach (var activeUpgrade in constructedUpgrades)
            {
                if (!activeUpgrade.def.requiresPower)
                    continue;
                
                // Skip disabled upgrades (including toggled off ones)
                var disableReason = GetUpgradeDisableReason(activeUpgrade.def);
                if (disableReason != UpgradeDisableReason.None && disableReason != UpgradeDisableReason.NoPower)
                    continue;
                
                float basePower = activeUpgrade.def.basePowerConsumption * activeUpgrade.count;
                float actualPower = basePower;
                
                if (activeUpgrade.def.IsSmartTempModifier)
                {
                    float throttle = GetSmartUtilizationRatio(activeUpgrade.def);
                    actualPower = basePower * throttle;
                    sb.AppendLine($"  {activeUpgrade.def.label} x{activeUpgrade.count}: {actualPower:F0}W ({throttle * 100:F0}% of {basePower:F0}W)");
                }
                else
                {
                    sb.AppendLine($"  {activeUpgrade.def.label} x{activeUpgrade.count}: {actualPower:F0}W");
                }
                
                totalPower += actualPower;
            }
            
            sb.Insert(0, $"Power Usage: {totalPower:F0}W\n");
            return sb.ToString();
        }

        /// <summary>
        /// Called every tick to handle fuel consumption, power, thoughts, and bathroom.
        /// </summary>
        public override void CompTick()
        {
            ConsumeFuel();
            UpdatePowerConsumption();
            
            // Partial class tick methods
            TickThoughtApplication();
            TickBathroom();
        }
        
        // =====================================================
        // GUI Overlay (merged from CompMultipleBeds)
        // =====================================================
        
        /// <summary>
        /// Draws the owner labels for multi-bed staircases.
        /// Called from HarmonyPatches Building_Bed_DrawGUIOverlay_Patch.
        /// </summary>
        public override void DrawGUIOverlay()
        {
            var bed = parent as Building_Bed;
            if (bed == null)
                return;
            
            if (bed.Medical || Find.CameraDriver.CurrentZoom != 0)
                return;
            
            Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
            
            // Check for guest bed type (Hospitality mod compatibility)
            var guestBedType = Building_Bed_DrawGUIOverlay_Patch.guestBedType;
            
            if (!bed.OwnersForReading.Any() && (guestBedType == null 
                || !guestBedType.IsAssignableFrom(parent.def.thingClass)))
            {
                GenMapUI.DrawThingLabel(bed, "Unowned".Translate(), defaultThingLabelColor);
                return;
            }
            
            if (bed.OwnersForReading.Count == 1)
            {
                Pawn pawn = bed.OwnersForReading[0];
                pawn.CurrentBed(out var sleepingSpot);
                if ((!pawn.InBed() || pawn.CurrentBed() != bed || sleepingSpot == 0) 
                    && (!pawn.RaceProps.Animal || Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn)))
                {
                    GenMapUI.DrawThingLabel(parent, pawn.LabelShort, defaultThingLabelColor);
                }
                return;
            }
            
            // Multiple owners - draw each label with offset
            for (int i = 0; i < bed.OwnersForReading.Count; i++)
            {
                Pawn pawn = bed.OwnersForReading[i];
                GenMapUI.DrawThingLabel(GetMultiOwnersLabelScreenPosFor(i), pawn.LabelShort, defaultThingLabelColor);
            }
        }
        
        private Vector3 GetMultiOwnersLabelScreenPosFor(int slotIndex)
        {
            Vector3 drawPos = parent.DrawPos;
            float stepSize = 0.4f;
            float zValue = 0.2f + stepSize * slotIndex - 1;
            drawPos += new Vector3(0, 0, zValue);
            return drawPos.MapToUIPosition();
        }
        
        /// <summary>
        /// Updates the power consumption for the staircase based on active upgrades.
        /// </summary>
        private void UpdatePowerConsumption()
        {
            var powerComp = parent.GetComp<CompPowerTrader>();
            if (powerComp == null)
            {
                Log.ErrorOnce($"CompStaircaseUpgrades on {parent?.LabelCap ?? "unknown"} requires CompPowerTrader but none was found.", parent?.thingIDNumber ?? 0);
                return;
            }
            
            float totalPower = CalculateTotalPowerConsumption();
            
            // CompPowerTrader expects negative values for power consumption
            powerComp.PowerOutput = -totalPower;
        }

        /// <summary>
        /// Calculates and consumes fuel based on active upgrades and bed count.
        /// Controllable fueled temp changers scale consumption based on utilization ratio.
        /// Uncontrollable fueled changers use the old throttling behavior.
        /// </summary>
        private void ConsumeFuel()
        {
            if (parent?.Map == null || constructedUpgrades == null || constructedUpgrades.Count == 0)
            {
                UpdateFuelConsumptionRate(0f);
                return;
            }

            // Get the CompRefuelable from parent
            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable == null)
            {
                UpdateFuelConsumptionRate(0f);
                return;
            }

            // Get current bed count
            int currentBedCount = BedCount;
            if (currentBedCount <= 0)
            {
                UpdateFuelConsumptionRate(0f);
                return;
            }

            // Get current outdoor temperature for uncontrollable throttling
            float currentOutdoorTemp = parent.Map.mapTemperature.OutdoorTemp;
            
            // Get utilization ratio for controllable fueled changers
            float utilizationRatio = GetFueledUtilizationRatio();
            
            // Calculate total fuel to consume
            float totalFuelToConsume = 0f;
            
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs().Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            foreach (var activeUpgrade in activeUpgrades)
            {
                if (activeUpgrade.def.fuelPerBed <= 0f)
                    continue;
                
                float consumption = activeUpgrade.def.fuelPerBed * currentBedCount / 60000f; // Convert per tick
                
                // Check if this is a controllable fueled temp changer
                if (activeUpgrade.def.followsDesiredTemp && activeUpgrade.def.heatOffset > 0f)
                {
                    // Scale consumption by utilization ratio (0% to 100%)
                    consumption *= utilizationRatio;
                }
                else
                {
                    // Uncontrollable - use old throttling behavior
                    // If it's already hot outside and we're heating, throttle to 50%
                    if (activeUpgrade.def.heatOffset > 0f && currentOutdoorTemp > activeUpgrade.def.maxHeatCap)
                    {
                        consumption *= 0.5f;
                    }
                    
                    // If it's already cold outside and we're cooling, throttle to 50%
                    if (activeUpgrade.def.coolOffset > 0f && currentOutdoorTemp < activeUpgrade.def.minCoolCap)
                    {
                        consumption *= 0.5f;
                    }
                }
                
                totalFuelToConsume += consumption;
            }
            
            // Update the display rate (per day, not per tick)
            // CompRefuelable will automatically consume fuel based on this rate in its CompTick()
            // We do NOT manually call refuelable.ConsumeFuel() as that would cause double consumption
            UpdateFuelConsumptionRate(totalFuelToConsume * 60000f);
        }

        /// <summary>
        /// Updates the fuel consumption rate in the Props so the inspect string shows correct time remaining.
        /// CompRefuelable automatically consumes fuel based on Props.fuelConsumptionRate in its CompTick().
        /// We dynamically update this rate based on bed count and throttling conditions.
        /// </summary>
        private void UpdateFuelConsumptionRate(float ratePerDay)
        {
            if (cachedFuelConsumptionRate != ratePerDay)
            {
                cachedFuelConsumptionRate = ratePerDay;
                var refuelable = parent.GetComp<CompRefuelable>();
                if (refuelable?.Props != null)
                {
                    refuelable.Props.fuelConsumptionRate = ratePerDay;
                }
            }
        }
        
        // =====================================================
        // Comfort and Sleep Effectiveness Calculations
        // =====================================================
        
        /// <summary>
        /// Calculates the total comfort bonus from all active upgrades.
        /// Each unique upgrade type contributes only once (not per bed).
        /// </summary>
        /// <returns>Total comfort bonus to add to the staircase's base comfort</returns>
        public float GetTotalComfortBonus()
        {
            if (constructedUpgrades == null || constructedUpgrades.Count == 0)
                return 0f;
            
            float totalBonus = 0f;
            
            // Get distinct active upgrade defs - each upgrade type only contributes once
            var activeUpgradeDefs = GetActiveUpgradeDefs();
            
            foreach (var upgradeDef in activeUpgradeDefs)
            {
                totalBonus += upgradeDef.comfortBonus;
            }
            
            return totalBonus;
        }
        
        /// <summary>
        /// Calculates the total sleep effectiveness bonus from all active upgrades.
        /// Each unique upgrade type contributes only once (not per bed).
        /// </summary>
        /// <returns>Total sleep effectiveness bonus to add to the staircase's base BedRestEffectiveness</returns>
        public float GetTotalSleepEffectivenessBonus()
        {
            if (constructedUpgrades == null || constructedUpgrades.Count == 0)
                return 0f;
            
            float totalBonus = 0f;
            
            // Get distinct active upgrade defs - each upgrade type only contributes once
            var activeUpgradeDefs = GetActiveUpgradeDefs();
            
            foreach (var upgradeDef in activeUpgradeDefs)
            {
                totalBonus += upgradeDef.sleepEffectivenessBonus;
            }
            
            return totalBonus;
        }
        
        // =====================================================
        // Destruction and Deconstruction Handling
        // =====================================================
        
        /// <summary>
        /// Health percentage threshold below which fire damage triggers explosions.
        /// </summary>
        private const float ExplosionHealthThreshold = 0.25f;
        
        /// <summary>
        /// Watt-days of stored energy per cell of explosion radius.
        /// 600Wd = 1x1, 1200Wd = 2x2, etc.
        /// </summary>
        private const float EnergyPerExplosionCell = 600f;
        
        /// <summary>
        /// Maximum explosion radius (7x7 area = radius of ~3.5).
        /// </summary>
        private const float MaxExplosionRadius = 3.5f;
        
        /// <summary>
        /// Flag to prevent multiple explosion triggers while health remains below threshold.
        /// </summary>
        private bool hasExplodedFromFire = false;
        
        /// <summary>
        /// Called when the parent thing is destroyed. Handles material refunds and subbuilding cleanup.
        /// </summary>
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            
            // Immediately clean up subbuildings
            DestroyLinkedBattery();
            DestroyLinkedBathroom();
            
            // If destroyed by damage (not deconstructed), collapse roofs in a 10x10 area
            // Only applies to upstairs staircases, not basements
            if (mode == DestroyMode.KillFinalize && previousMap != null)
            {
                SecondFloorModExtension ext = parent.def.GetModExtension<SecondFloorModExtension>();
                if (ext == null || ext.floorLevel != StaircaseFloorLevel.Basement)
                {
                    CollapseNearbyRoofs(previousMap);
                }
            }
            
            // Determine refund percentage based on destruction mode
            float refundPercent = 0f;
            if (mode == DestroyMode.Deconstruct)
            {
                refundPercent = 0.75f;
            }
            else if (mode == DestroyMode.KillFinalize)
            {
                refundPercent = 0.37f;
            }
            
            // Refund materials for all constructed upgrades
            if (refundPercent > 0f && previousMap != null)
            {
                RefundAllUpgradeMaterials(refundPercent, previousMap);
            }
        }
        
        /// <summary>
        /// Collapses constructed roofs in a circular area (radius 5) around the destroyed staircase.
        /// </summary>
        private void CollapseNearbyRoofs(Map map)
        {
            List<IntVec3> cellsToCollapse = new List<IntVec3>();
            
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 5f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                RoofDef roof = cell.GetRoof(map);
                if (roof == RoofDefOf.RoofConstructed)
                {
                    cellsToCollapse.Add(cell);
                }
            }
            
            if (cellsToCollapse.Count > 0)
            {
                RoofCollapserImmediate.DropRoofInCells(cellsToCollapse, map);
            }
        }
        
        /// <summary>
        /// Refunds materials for all constructed upgrades at the specified percentage.
        /// </summary>
        private void RefundAllUpgradeMaterials(float refundPercent, Map map)
        {
            if (constructedUpgrades == null || constructedUpgrades.Count == 0)
            {
                return;
            }
            
            IntVec3 dropPos = parent.Position;
            
            foreach (var activeUpgrade in constructedUpgrades)
            {
                if (activeUpgrade.def == null || activeUpgrade.count <= 0)
                {
                    continue;
                }
                
                int constructedCount = activeUpgrade.count;
                
                // Handle stuff-based materials (costStuffCount)
                if (activeUpgrade.stuff != null && activeUpgrade.def.upgradeBuildingDef?.costStuffCount > 0)
                {
                    int baseCost = activeUpgrade.def.upgradeBuildingDef.costStuffCount;
                    int totalCost = baseCost * constructedCount;
                    int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);
                    
                    if (refundAmount > 0)
                    {
                        Thing refundThing = ThingMaker.MakeThing(activeUpgrade.stuff);
                        refundThing.stackCount = refundAmount;
                        GenPlace.TryPlaceThing(refundThing, dropPos, map, ThingPlaceMode.Near);
                    }
                }
                
                // Handle costList items
                if (activeUpgrade.def.upgradeBuildingDef?.costList != null)
                {
                    foreach (var cost in activeUpgrade.def.upgradeBuildingDef.costList)
                    {
                        int totalCost = cost.count * constructedCount;
                        int refundAmount = Mathf.FloorToInt(totalCost * refundPercent);
                        
                        if (refundAmount > 0)
                        {
                            Thing refundThing = ThingMaker.MakeThing(cost.thingDef);
                            refundThing.stackCount = refundAmount;
                            GenPlace.TryPlaceThing(refundThing, dropPos, map, ThingPlaceMode.Near);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Called after damage is applied to the parent. Triggers explosions for battery/special upgrades
        /// when damaged by fire below the health threshold.
        /// </summary>
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            
            // Only trigger on fire damage
            if (dinfo.Def != DamageDefOf.Flame)
            {
                return;
            }
            
            // Check if we've already exploded
            if (hasExplodedFromFire)
            {
                return;
            }
            
            // Check health threshold
            float healthPercent = (float)parent.HitPoints / (float)parent.MaxHitPoints;
            if (healthPercent >= ExplosionHealthThreshold)
            {
                return;
            }
            
            // Check if we have any explosive upgrades installed
            if (!HasAnyExplosiveUpgrade())
            {
                return;
            }
            
            // Mark as exploded and trigger explosions
            hasExplodedFromFire = true;
            TriggerUpgradeExplosions();
        }
        
        /// <summary>
        /// Returns true if any installed upgrade can cause an explosion (battery or special def upgrades).
        /// </summary>
        private bool HasAnyExplosiveUpgrade()
        {
            if (constructedUpgrades == null)
            {
                return false;
            }
            
            foreach (var upgrade in constructedUpgrades)
            {
                // Battery upgrades can explode
                if (upgrade.def.IsBatteryUpgrade)
                {
                    return true;
                }
                
                // Upgrades with linked buildings (bathroom, etc.) cause small explosions
                if (upgrade.def.linkedBathroomDef != null)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Triggers explosions for all installed explosive upgrades.
        /// Battery upgrades explode based on stored energy, other special upgrades cause 1x1 explosions.
        /// </summary>
        private void TriggerUpgradeExplosions()
        {
            if (!parent.Spawned || parent.Map == null)
            {
                return;
            }
            
            Map map = parent.Map;
            IntVec3 position = parent.Position;
            
            foreach (var upgrade in constructedUpgrades)
            {
                if (upgrade.def.IsBatteryUpgrade)
                {
                    // Calculate explosion radius based on stored energy
                    float storedEnergy = StoredEnergy;
                    float explosionRadius = CalculateExplosionRadius(storedEnergy);
                    
                    if (explosionRadius > 0f)
                    {
                        GenExplosion.DoExplosion(
                            center: position,
                            map: map,
                            radius: explosionRadius,
                            damType: DamageDefOf.Bomb,
                            instigator: parent,
                            damAmount: -1, // Use default for damage type
                            armorPenetration: -1f,
                            explosionSound: null,
                            weapon: null,
                            projectile: null,
                            intendedTarget: null,
                            postExplosionSpawnThingDef: null,
                            postExplosionSpawnChance: 0f,
                            postExplosionSpawnThingCount: 0,
                            postExplosionGasType: null,
                            applyDamageToExplosionCellsNeighbors: false,
                            preExplosionSpawnThingDef: null,
                            preExplosionSpawnChance: 0f,
                            preExplosionSpawnThingCount: 0,
                            chanceToStartFire: 0.5f,
                            damageFalloff: true
                        );
                        
                        // Drain the battery after explosion
                        LinkedBatteryComp?.SetStoredEnergyPct(0f);
                    }
                }
                else if (upgrade.def.linkedBathroomDef != null)
                {
                    // Non-power-storing special upgrades cause a 1x1 explosion
                    GenExplosion.DoExplosion(
                        center: position,
                        map: map,
                        radius: 0.5f, // 1x1 area
                        damType: DamageDefOf.Bomb,
                        instigator: parent,
                        damAmount: -1,
                        armorPenetration: -1f,
                        explosionSound: null,
                        weapon: null,
                        projectile: null,
                        intendedTarget: null,
                        postExplosionSpawnThingDef: null,
                        postExplosionSpawnChance: 0f,
                        postExplosionSpawnThingCount: 0,
                        postExplosionGasType: null,
                        applyDamageToExplosionCellsNeighbors: false,
                        preExplosionSpawnThingDef: null,
                        preExplosionSpawnChance: 0f,
                        preExplosionSpawnThingCount: 0,
                        chanceToStartFire: 0.25f,
                        damageFalloff: false
                    );
                }
            }
        }
        
        /// <summary>
        /// Calculates the explosion radius based on stored energy.
        /// 600Wd = 1 cell area (radius ~0.5), 1200Wd = 4 cell area (radius ~1.1), etc.
        /// Capped at 7x7 area (radius 3.5).
        /// </summary>
        private float CalculateExplosionRadius(float storedEnergy)
        {
            if (storedEnergy <= 0f)
            {
                return 0f;
            }
            
            // Calculate area: storedEnergy / 600Wd per cell
            float explosionArea = storedEnergy / EnergyPerExplosionCell;
            
            // Convert area to radius: area = pi * r^2, so r = sqrt(area / pi)
            // For simplicity, use r = sqrt(area) which gives slightly larger explosions
            float radius = Mathf.Sqrt(explosionArea);
            
            // Cap at maximum radius
            return Mathf.Min(radius, MaxExplosionRadius);
        }
    }
}
