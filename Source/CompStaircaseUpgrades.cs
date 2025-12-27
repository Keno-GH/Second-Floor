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
        OutOfFuel,
        InsufficientCount // For onePerBed upgrades
        // Future: Add more reasons here as needed
    }
    public class CompProperties_StaircaseUpgrades : CompProperties
    {
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
        
        public ActiveUpgrade()
        {
        }
        
        public ActiveUpgrade(StaircaseUpgradeDef def, ThingDef stuff)
        {
            this.def = def;
            this.stuff = stuff;
            this.count = 1;
        }
        
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref count, "count", 1);
        }
    }

    public class CompStaircaseUpgrades : ThingComp
    {
        public List<ActiveUpgrade> constructedUpgrades = new List<ActiveUpgrade>();
        private float cachedFuelConsumptionRate = 0f;
        
        // Legacy field for backward compatibility
        private List<StaircaseUpgradeDef> upgrades;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref constructedUpgrades, "constructedUpgrades", LookMode.Deep);
            
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
        
        public float GetTotalSpace()
        {
            if (parent.GetRoom() != null)
            {
                return parent.GetRoom().CellCount;
            }
            return 0f;
        }

        public float GetUsedSpace()
        {
            float used = 0;
            foreach(var activeUpgrade in constructedUpgrades)
            {
                used += activeUpgrade.def.spaceCost;
                used += activeUpgrade.def.spaceCostPerBed * (parent.GetComp<CompMultipleBeds>()?.bedCount ?? 1);
            }
            return used;
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
                    var bedsComp = parent.GetComp<CompMultipleBeds>();
                    int bedCount = bedsComp?.bedCount ?? 1;
                    if (constructedUpgrade.count < bedCount)
                    {
                        return UpgradeDisableReason.InsufficientCount;
                    }
                }
            }

            return UpgradeDisableReason.None;
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

            return refundInfo;
        }

        /// <summary>
        /// Helper method to get bed count for an upgrade. Sets to 1 if not applicable to the upgrade.
        /// </summary>
        private int GetBedCount(StaircaseUpgradeDef def)
        {
            // Get bed count to calculate total cost if applicable
            int bedCount = 1;
            bool onePerBed = def.upgradeBuildingDef.GetModExtension<StaircaseUpgradeExtension>()?.onePerBed ?? true;
            if (onePerBed)
            {
                CompMultipleBeds bedsComp = parent.GetComp<CompMultipleBeds>();
                bedCount = bedsComp?.bedCount ?? 1;
            }

            return bedCount;
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
        /// Calculates the virtual temperature for the Second Floor based on active upgrades.
        /// Applies insulation, heating, and cooling effects in sequence.
        /// </summary>
        /// <returns>The calculated virtual temperature</returns>
        public float CalculateVirtualTemperature()
        {
            if (parent?.Map == null) return 21f; // Default temperature if not spawned

            // Step 1: Start with outdoor temperature
            float temp = parent.Map.mapTemperature.OutdoorTemp;

            // Step 2: Apply Insulation
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs().Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            float totalInsulation = activeUpgrades.Sum(au => au.def.insulationAdjustment);
            if (totalInsulation > 0)
            {
                // Get the average insulation target (weighted by insulation strength)
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

                // Calculate difference and apply correction
                float diff = insulationTarget - temp;
                float correction = diff;
                if (correction > totalInsulation)
                    correction = totalInsulation;
                else if (correction < -totalInsulation)
                    correction = -totalInsulation;
                
                temp += correction;
            }

            // Step 3: Apply Heating
            float totalHeat = activeUpgrades.Sum(au => au.def.heatOffset);
            if (totalHeat > 0)
            {
                // Find global max cap (highest maxHeatCap among heaters)
                float globalMaxCap = 100f; // Default high value
                var heatersWithCap = activeUpgrades.Where(au => au.def.heatOffset > 0).ToList();
                if (heatersWithCap.Any())
                {
                    globalMaxCap = heatersWithCap.Max(au => au.def.maxHeatCap);
                }

                float heatedTemp = temp + totalHeat;
                if (heatedTemp > globalMaxCap)
                    heatedTemp = globalMaxCap;
                
                // Ensure we don't cool down a hot room with a heater
                if (heatedTemp > temp)
                    temp = heatedTemp;
            }

            // Step 4: Apply Cooling
            float totalCool = activeUpgrades.Sum(au => au.def.coolOffset);
            if (totalCool > 0)
            {
                // Find global min cap (lowest minCoolCap among coolers)
                float globalMinCap = -273f; // Default low value
                var coolersWithCap = activeUpgrades.Where(au => au.def.coolOffset > 0).ToList();
                if (coolersWithCap.Any())
                {
                    globalMinCap = coolersWithCap.Min(au => au.def.minCoolCap);
                }

                float cooledTemp = temp - totalCool;
                if (cooledTemp < globalMinCap)
                    cooledTemp = globalMinCap;
                
                // Ensure we don't heat up a cold room with a cooler
                if (cooledTemp < temp)
                    temp = cooledTemp;
            }

            // Step 5: Return final temperature
            return temp;
        }

        /// <summary>
        /// Called every tick to handle fuel consumption based on active upgrades.
        /// </summary>
        public override void CompTick()
        {
            ConsumeFuel();
        }

        /// <summary>
        /// Calculates and consumes fuel based on active upgrades and bed count.
        /// Applies throttling when temperature is already outside the desired range.
        /// </summary>
        private void ConsumeFuel()
        {
            if (parent?.Map == null || constructedUpgrades == null || constructedUpgrades.Count == 0)
            {
                UpdateFuelConsumptionRate(1f);
                return;
            }

            // Get the CompRefuelable from parent
            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable == null)
            {
                UpdateFuelConsumptionRate(1f);
                return;
            }

            // Get current bed count
            var bedsComp = parent.GetComp<CompMultipleBeds>();
            if (bedsComp == null)
            {
                UpdateFuelConsumptionRate(1f);
                return;
            }
            
            int currentBedCount = bedsComp.bedCount;
            if (currentBedCount <= 0)
            {
                UpdateFuelConsumptionRate(1f);
                return;
            }

            // Get current outdoor temperature
            float currentOutdoorTemp = parent.Map.mapTemperature.OutdoorTemp;
            
            // Calculate total fuel to consume
            float totalFuelToConsume = 0f;
            
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs().Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            foreach (var activeUpgrade in activeUpgrades)
            {
                if (activeUpgrade.def.fuelPerBed <= 0f)
                    continue;
                
                float consumption = activeUpgrade.def.fuelPerBed * currentBedCount / 60000f; // Convert per tick
                
                // Apply throttling based on temperature
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
    }
}
