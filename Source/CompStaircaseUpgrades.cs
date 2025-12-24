using Verse;
using RimWorld;
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

    public class CompStaircaseUpgrades : ThingComp
    {
        public List<StaircaseUpgradeDef> upgrades = new List<StaircaseUpgradeDef>();
        private float cachedFuelConsumptionRate = 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
            if (upgrades == null)
            {
                upgrades = new List<StaircaseUpgradeDef>();
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
            foreach(var u in upgrades)
            {
                used += u.spaceCost;
            }
            return used;
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
            float totalInsulation = upgrades.Sum(u => u.insulationAdjustment);
            if (totalInsulation > 0)
            {
                // Get the average insulation target (weighted by insulation strength)
                float weightedTargetSum = 0f;
                float weightSum = 0f;
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.insulationAdjustment > 0)
                    {
                        weightedTargetSum += upgrade.insulationTarget * upgrade.insulationAdjustment;
                        weightSum += upgrade.insulationAdjustment;
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
            float totalHeat = upgrades.Sum(u => u.heatOffset);
            if (totalHeat > 0)
            {
                // Find global max cap (highest maxHeatCap among heaters)
                float globalMaxCap = 100f; // Default high value
                var heatersWithCap = upgrades.Where(u => u.heatOffset > 0).ToList();
                if (heatersWithCap.Any())
                {
                    globalMaxCap = heatersWithCap.Max(u => u.maxHeatCap);
                }

                float heatedTemp = temp + totalHeat;
                if (heatedTemp > globalMaxCap)
                    heatedTemp = globalMaxCap;
                
                // Ensure we don't cool down a hot room with a heater
                if (heatedTemp > temp)
                    temp = heatedTemp;
            }

            // Step 4: Apply Cooling
            float totalCool = upgrades.Sum(u => u.coolOffset);
            if (totalCool > 0)
            {
                // Find global min cap (lowest minCoolCap among coolers)
                float globalMinCap = -273f; // Default low value
                var coolersWithCap = upgrades.Where(u => u.coolOffset > 0).ToList();
                if (coolersWithCap.Any())
                {
                    globalMinCap = coolersWithCap.Min(u => u.minCoolCap);
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
            if (parent?.Map == null || upgrades == null || upgrades.Count == 0)
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
            
            foreach (var upgrade in upgrades)
            {
                if (upgrade.fuelPerBed <= 0f)
                    continue;
                
                float consumption = upgrade.fuelPerBed * currentBedCount / 60000f; // Convert per tick
                
                // Apply throttling based on temperature
                // If it's already hot outside and we're heating, throttle to 50%
                if (upgrade.heatOffset > 0f && currentOutdoorTemp > upgrade.maxHeatCap)
                {
                    consumption *= 0.5f;
                }
                
                // If it's already cold outside and we're cooling, throttle to 50%
                if (upgrade.coolOffset > 0f && currentOutdoorTemp < upgrade.minCoolCap)
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
