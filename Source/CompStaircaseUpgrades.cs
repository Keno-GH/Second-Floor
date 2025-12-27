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
        ToggledOff,      // Manually toggled off by player
        OutOfFuel,
        NoPower,
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

    public class CompStaircaseUpgrades : ThingComp
    {
        public List<ActiveUpgrade> constructedUpgrades = new List<ActiveUpgrade>();
        private float cachedFuelConsumptionRate = 0f;
        
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
        /// Cached power consumption value for display
        /// </summary>
        private float cachedTotalPowerConsumption = 0f;
        
        // Legacy field for backward compatibility
        private List<StaircaseUpgradeDef> upgrades;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref constructedUpgrades, "constructedUpgrades", LookMode.Deep);
            Scribe_Values.Look(ref targetTemperature, "targetTemperature", 21f);
            
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
        
        /// <summary>
        /// Returns true if any constructed upgrade requires power.
        /// </summary>
        public bool HasAnyPowerRequiringUpgrade()
        {
            return constructedUpgrades.Any(au => au.def.requiresPower);
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
        /// Gets the current total power consumption for display purposes.
        /// </summary>
        public float CurrentPowerConsumption => cachedTotalPowerConsumption;
        
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
        /// Calculates the virtual temperature for the Second Floor based on active upgrades.
        /// Applies insulation, dumb heaters/coolers, then smart temp modifiers.
        /// </summary>
        /// <returns>The calculated virtual temperature</returns>
        public float CalculateVirtualTemperature()
        {
            if (parent?.Map == null) return 21f; // Default temperature if not spawned

            // Get base temperature (outdoor + insulation + dumb heaters/coolers)
            float temp = GetBaseTemperature();
            
            // Now apply smart temperature modifiers
            List<ActiveUpgrade> activeUpgrades = GetActiveUpgradeDefs()
                .Select(def => constructedUpgrades.First(au => au.def == def)).ToList();
            
            var smartTempModifiers = activeUpgrades.Where(au => au.def.IsSmartTempModifier).ToList();
            
            if (!smartTempModifiers.Any() || !HasPower())
            {
                return temp;
            }
            
            // Smart temp modifiers try to reach the target temperature
            float tempDiff = targetTemperature - temp;
            
            if (Mathf.Abs(tempDiff) < 0.1f)
            {
                // Already at target, no adjustment needed
                return temp;
            }
            
            // Calculate total heating and cooling capacity
            float totalHeatingCapacity = 0f;
            float totalCoolingCapacity = 0f;
            
            foreach (var mod in smartTempModifiers)
            {
                int upgradeCount = constructedUpgrades.First(au => au.def == mod.def).count;
                
                // Heating capacity
                if (mod.def.smartTempModifierType == TempModifierType.HeaterOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    // Degrees per 100W * (power / 100) * count
                    totalHeatingCapacity += mod.def.smartHeatEfficiency * (mod.def.basePowerConsumption / 100f) * upgradeCount;
                }
                
                // Cooling capacity
                if (mod.def.smartTempModifierType == TempModifierType.CoolerOnly || 
                    mod.def.smartTempModifierType == TempModifierType.DualMode)
                {
                    totalCoolingCapacity += mod.def.smartCoolEfficiency * (mod.def.basePowerConsumption / 100f) * upgradeCount;
                }
            }
            
            // Apply heating or cooling based on need
            if (tempDiff > 0f && totalHeatingCapacity > 0f)
            {
                // Need to heat
                float heatToAdd = Mathf.Min(tempDiff, totalHeatingCapacity);
                temp += heatToAdd;
            }
            else if (tempDiff < 0f && totalCoolingCapacity > 0f)
            {
                // Need to cool
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
            
            // Get base temperature for smart temp modifier calculations
            float baseTemp = GetBaseTemperature();
            
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
                    float throttle = CalculateSmartTempThrottle(activeUpgrade.def, baseTemp);
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
            float baseTemp = GetBaseTemperature();
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
                    float throttle = CalculateSmartTempThrottle(activeUpgrade.def, baseTemp);
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
        /// Called every tick to handle fuel consumption based on active upgrades.
        /// </summary>
        public override void CompTick()
        {
            ConsumeFuel();
            UpdatePowerConsumption();
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
