using RimWorld;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// An invisible battery building that is spawned by a staircase when a battery upgrade is installed.
    /// This building is linked to its parent staircase and will be destroyed when the staircase is destroyed.
    /// 
    /// Using a real sub-building with vanilla CompPowerBattery ensures maximum compatibility with other mods.
    /// </summary>
    public class Building_StaircaseBattery : Building
    {
        /// <summary>
        /// Reference to the parent staircase that spawned this battery.
        /// </summary>
        public Thing parentStaircase;
        
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            // If spawning fresh (not loading), find the parent staircase at our position
            if (!respawningAfterLoad && parentStaircase == null)
            {
                parentStaircase = FindStaircaseAtPosition(map);
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref parentStaircase, "parentStaircase");
        }
        
        protected override void Tick()
        {
            base.Tick();
            
            // Self-destruct if parent staircase is gone
            if (parentStaircase == null || parentStaircase.Destroyed || !parentStaircase.Spawned)
            {
                Destroy(DestroyMode.Vanish);
            }
        }
        
        /// <summary>
        /// Find a staircase building at this battery's position.
        /// </summary>
        private Thing FindStaircaseAtPosition(Map map)
        {
            foreach (Thing thing in Position.GetThingList(map))
            {
                if (thing.def.defName == "SF_Upstairs" || thing.def.defName == "SF_Basement")
                {
                    return thing;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Gets the battery comp for external access.
        /// </summary>
        public CompPowerBattery BatteryComp => GetComp<CompPowerBattery>();
        
        /// <summary>
        /// Override to prevent selection - this is an invisible building.
        /// </summary>
        public override string LabelMouseover => parentStaircase?.LabelMouseover ?? base.LabelMouseover;
    }
}
