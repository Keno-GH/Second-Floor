using RimWorld;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// A special mineable rock used for basement expansion.
    /// When mined, it grants bonus space to the linked basement staircase.
    /// If the mining designation is canceled, all rocks in the batch are removed.
    /// </summary>
    public class Building_BasementExpansionRock : Mineable
    {
        /// <summary>
        /// Reference to the basement staircase this rock is linked to.
        /// </summary>
        public Thing linkedBasement;
        
        /// <summary>
        /// Batch ID to group rocks spawned together.
        /// Used to cancel all rocks when one designation is removed.
        /// </summary>
        public int batchId;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedBasement, "linkedBasement");
            Scribe_Values.Look(ref batchId, "batchId", 0);
        }
        
        /// <summary>
        /// Called by Harmony patch when this rock is fully mined.
        /// Notifies the linked basement to grant bonus space.
        /// </summary>
        public void NotifyMiningComplete(Pawn pawn)
        {
            if (linkedBasement != null && !linkedBasement.Destroyed)
            {
                var upgradesComp = linkedBasement.TryGetComp<CompStaircaseUpgrades>();
                upgradesComp?.OnRockMined(this);
            }
        }
        
        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            
            if (linkedBasement != null && !linkedBasement.Destroyed)
            {
                var upgradesComp = linkedBasement.TryGetComp<CompStaircaseUpgrades>();
                if (upgradesComp != null)
                {
                    int currentProgress = upgradesComp.MinedCountInBatch;
                    int total = 5;
                    string progressText = "SF_ExcavationProgress".Translate(currentProgress, total);
                    
                    if (string.IsNullOrEmpty(baseString))
                        return progressText;
                    return baseString + "\n" + progressText;
                }
            }
            
            return baseString;
        }
    }
}
