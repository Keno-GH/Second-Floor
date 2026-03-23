using RimWorld;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// An invisible bathroom building that is spawned by a staircase when a bathroom upgrade is installed.
    /// This building is linked to its parent staircase and provides pipe connectivity for DBH integration.
    /// The building should have DBH's CompPipe added via XML for pipe grid connection.
    /// </summary>
    public class Building_StaircaseBathroom : Building
    {
        /// <summary>
        /// Reference to the parent staircase that spawned this bathroom.
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
            
            // Only check parent every 250 ticks — no need to check every single tick
            if (!this.IsHashIntervalTick(250))
                return;
            
            // Self-destruct if parent staircase is gone
            if (parentStaircase == null || parentStaircase.Destroyed || !parentStaircase.Spawned)
            {
                Destroy(DestroyMode.Vanish);
            }
        }
        
        /// <summary>
        /// Find a staircase building at this bathroom's position.
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
        /// Gets the DBH CompPipe if available (via reflection).
        /// </summary>
        public object CompPipe => DBHReflectionHelper.GetCompPipe(this);
        
        /// <summary>
        /// Gets the plumbing net this bathroom is connected to.
        /// </summary>
        public object PlumbingNet => DBHReflectionHelper.GetPlumbingNet(CompPipe);
        
        /// <summary>
        /// Returns true if this bathroom is connected to a plumbing network with water.
        /// </summary>
        public bool HasWaterConnection
        {
            get
            {
                var net = PlumbingNet;
                return net != null && DBHReflectionHelper.GetWaterStorage(net) > 0;
            }
        }
        
        /// <summary>
        /// Returns true if this bathroom has hot water available.
        /// </summary>
        public bool HasHotWater
        {
            get
            {
                var net = PlumbingNet;
                return net != null && DBHReflectionHelper.GetHotWaterStorage(net) > 0;
            }
        }
        
        /// <summary>
        /// Attempts to use water from the connected plumbing network.
        /// </summary>
        public bool TryUseWater(float amount)
        {
            var net = PlumbingNet;
            return DBHReflectionHelper.TryPullWater(net, amount);
        }
        
        /// <summary>
        /// Attempts to use hot water from the connected plumbing network.
        /// </summary>
        public bool TryUseHotWater(float amount)
        {
            var net = PlumbingNet;
            return DBHReflectionHelper.TryPullHotWater(net, amount);
        }
        
        /// <summary>
        /// Pushes sewage to the connected plumbing network.
        /// </summary>
        public bool PushSewage(float amount)
        {
            var net = PlumbingNet;
            return DBHReflectionHelper.TryPushSewage(net, amount);
        }
        
        /// <summary>
        /// Checks if enough water is available for the specified amount.
        /// </summary>
        public bool HasEnoughWater(float amount)
        {
            var net = PlumbingNet;
            return DBHReflectionHelper.HasEnoughWater(net, amount);
        }
        
        /// <summary>
        /// Checks if enough hot water is available for the specified amount.
        /// </summary>
        public bool HasEnoughHotWater(float amount)
        {
            var net = PlumbingNet;
            return DBHReflectionHelper.HasEnoughHotWater(net, amount);
        }
        
        /// <summary>
        /// Override to prevent selection - this is an invisible building.
        /// </summary>
        public override string LabelMouseover => parentStaircase?.LabelMouseover ?? base.LabelMouseover;
    }
}
