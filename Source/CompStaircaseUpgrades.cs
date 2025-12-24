using Verse;
using RimWorld;
using System.Collections.Generic;

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
                if (parent?.Map == null) return 21f; // Default temperature if not spawned
                
                // Base temperature is ALWAYS outdoor temperature
                float temperature = parent.Map.mapTemperature.OutdoorTemp;
                
                // Future: Upgrades could add temperature offsets here
                // foreach (var upgrade in upgrades)
                // {
                //     temperature += upgrade.temperatureOffset;
                // }
                
                return temperature;
            }
        }
    }
}
