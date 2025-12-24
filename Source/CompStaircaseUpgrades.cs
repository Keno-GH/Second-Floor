using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace SecondFloor
{
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
        
        public ActiveUpgrade()
        {
        }
        
        public ActiveUpgrade(StaircaseUpgradeDef def, ThingDef stuff)
        {
            this.def = def;
            this.stuff = stuff;
        }
        
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuff, "stuff");
        }
    }

    public class CompStaircaseUpgrades : ThingComp
    {
        public List<ActiveUpgrade> activeUpgrades = new List<ActiveUpgrade>();
        private float cachedFuelConsumptionRate = 0f;
        
        // Legacy field for backward compatibility
        private List<StaircaseUpgradeDef> upgrades;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref activeUpgrades, "activeUpgrades", LookMode.Deep);
            
            // Legacy support: load old "upgrades" list and convert to activeUpgrades
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
            }
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeUpgrades == null)
                {
                    activeUpgrades = new List<ActiveUpgrade>();
                }
                
                // Convert legacy upgrades to activeUpgrades
                if (upgrades != null && upgrades.Count > 0)
                {
                    foreach (var upgradeDef in upgrades)
                    {
                        if (!activeUpgrades.Any(au => au.def == upgradeDef))
                        {
                            activeUpgrades.Add(new ActiveUpgrade(upgradeDef, null));
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
            foreach(var activeUpgrade in activeUpgrades)
            {
                used += activeUpgrade.def.spaceCost;
            }
            return used;
        }
        
        /// <summary>
        /// Helper method to check if an upgrade is installed
        /// </summary>
        public bool HasUpgrade(StaircaseUpgradeDef def)
        {
            return activeUpgrades.Any(au => au.def == def);
        }
        
        /// <summary>
        /// Helper method to get all installed upgrade defs
        /// </summary>
        public List<StaircaseUpgradeDef> GetUpgradeDefs()
        {
            return activeUpgrades.Select(au => au.def).ToList();
        }
        
        /// <summary>
        /// Adds an upgrade with optional stuff
        /// </summary>
        public void AddUpgrade(StaircaseUpgradeDef def, ThingDef stuff)
        {
            if (!HasUpgrade(def))
            {
                activeUpgrades.Add(new ActiveUpgrade(def, stuff));
            }
        }
        
        /// <summary>
        /// Removes an upgrade
        /// </summary>
        public void RemoveUpgrade(StaircaseUpgradeDef def)
        {
            activeUpgrades.RemoveAll(au => au.def == def);
        }
        
        /// <summary>
        /// Removes an upgrade and refunds materials based on the stuff used and bed count.
        /// Returns the refunded material information for messaging.
        /// </summary>
        public string RemoveUpgradeWithRefund(StaircaseUpgradeDef def, float refundPercent = 0.75f)
        {
            // Find the active upgrade to get the stuff used
            ActiveUpgrade activeUpgrade = activeUpgrades.FirstOrDefault(au => au.def == def);
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
            activeUpgrades.RemoveAll(au => au.def == def);

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
            if (parent?.Map == null || activeUpgrades == null || activeUpgrades.Count == 0)
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
            var bedsComp = parent.GetComp<CompMultipleBeds>();
            if (bedsComp == null)
            {
                UpdateFuelConsumptionRate(0f);
                return;
            }
            
            int currentBedCount = bedsComp.bedCount;
            if (currentBedCount <= 0)
            {
                UpdateFuelConsumptionRate(0f);
                return;
            }

            // Get current outdoor temperature
            float currentOutdoorTemp = parent.Map.mapTemperature.OutdoorTemp;
            
            // Calculate total fuel to consume
            float totalFuelToConsume = 0f;
            
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
            UpdateFuelConsumptionRate(totalFuelToConsume * 60000f);
            
            // Consume the fuel
            if (totalFuelToConsume > 0f)
            {
                refuelable.ConsumeFuel(totalFuelToConsume);
            }
        }

        /// <summary>
        /// Updates the fuel consumption rate in the Props so the inspect string shows correct time remaining.
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
