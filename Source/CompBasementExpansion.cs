using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Component that manages basement space expansion through mining.
    /// Attached to basement staircases to track bonus space and excavation progress.
    /// </summary>
    public class CompBasementExpansion : ThingComp
    {
        /// <summary>
        /// Bonus space unlocked through completed mining batches.
        /// </summary>
        private int bonusSpace = 0;
        
        /// <summary>
        /// List of currently spawned expansion rocks in the active batch.
        /// </summary>
        private List<Thing> currentBatchRocks = new List<Thing>();
        
        /// <summary>
        /// Counter for how many rocks have been mined in the current batch.
        /// When this reaches 5, the bonus is applied.
        /// </summary>
        private int minedCountInBatch = 0;
        
        /// <summary>
        /// Incrementing batch ID to identify rock groups.
        /// </summary>
        private int nextBatchId = 0;
        
        public CompProperties_BasementExpansion Props => (CompProperties_BasementExpansion)props;
        
        /// <summary>
        /// Total space available (base + bonus).
        /// </summary>
        public int TotalSpace => Props.baseSpace + bonusSpace;
        
        /// <summary>
        /// Maximum possible space (base + max bonus).
        /// </summary>
        public int MaxSpace => Props.baseSpace + Props.maxBonusSpace;
        
        /// <summary>
        /// Current bonus space unlocked.
        /// </summary>
        public int BonusSpace => bonusSpace;
        
        /// <summary>
        /// Whether an excavation batch is currently in progress.
        /// </summary>
        public bool IsExcavationInProgress => currentBatchRocks.Count > 0;
        
        /// <summary>
        /// Whether the basement has reached maximum expansion.
        /// </summary>
        public bool IsMaxExpansion => bonusSpace >= Props.maxBonusSpace;
        
        /// <summary>
        /// Number of rocks mined in the current batch.
        /// </summary>
        public int MinedCountInBatch => minedCountInBatch;
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref bonusSpace, "bonusSpace", 0);
            Scribe_Values.Look(ref minedCountInBatch, "minedCountInBatch", 0);
            Scribe_Values.Look(ref nextBatchId, "nextBatchId", 0);
            Scribe_Collections.Look(ref currentBatchRocks, "currentBatchRocks", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (currentBatchRocks == null)
                {
                    currentBatchRocks = new List<Thing>();
                }
                // Remove any null references from destroyed rocks
                currentBatchRocks.RemoveAll(r => r == null || r.Destroyed);
            }
        }
        
        /// <summary>
        /// Finds the closest free cells to the staircase where excavation rocks can be spawned.
        /// Searches in expanding radius from the staircase center until enough cells are found.
        /// </summary>
        private List<IntVec3> FindFreeCellsAroundStaircase(int count)
        {
            Map map = parent.Map;
            List<IntVec3> freeCells = new List<IntVec3>();
            
            // Get all cells occupied by the staircase to exclude them
            CellRect staircaseRect = parent.OccupiedRect();
            
            // Search in expanding radius from the staircase center
            // Max radius of 20 should be more than enough
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 20f, true))
            {
                // Skip cells that are part of the staircase itself
                if (staircaseRect.Contains(cell))
                    continue;
                
                // Check if cell is valid
                if (!cell.InBounds(map))
                    continue;
                
                // Check if cell is standable (not blocked by walls, buildings, etc.)
                if (!cell.Standable(map))
                    continue;
                
                // Check if there's already an excavation rock here
                bool hasExcavationRock = cell.GetThingList(map).Any(t => t is Building_BasementExpansionRock);
                if (hasExcavationRock)
                    continue;
                
                freeCells.Add(cell);
                
                if (freeCells.Count >= count)
                    break;
            }
            
            return freeCells;
        }
        
        /// <summary>
        /// Spawns 5 expansion rocks on free cells around the staircase with mining designations.
        /// </summary>
        public void SpawnExpansionRocks()
        {
            if (IsExcavationInProgress || IsMaxExpansion)
                return;
            
            Map map = parent.Map;
            if (map == null)
                return;
            
            ThingDef rockDef = DefDatabase<ThingDef>.GetNamed("SF_BasementExpansionRock", false);
            if (rockDef == null)
            {
                Log.Error("[SecondFloor] Could not find SF_BasementExpansionRock ThingDef");
                return;
            }
            
            // Find 5 free cells around the staircase
            List<IntVec3> freeCells = FindFreeCellsAroundStaircase(5);
            
            if (freeCells.Count < 5)
            {
                Messages.Message("SF_NotEnoughFreeCells".Translate(freeCells.Count, 5), 
                    parent, MessageTypeDefOf.RejectInput);
                return;
            }
            
            int currentBatchId = nextBatchId++;
            minedCountInBatch = 0;
            currentBatchRocks.Clear();
            
            // Spawn rocks on the free cells
            foreach (IntVec3 cell in freeCells)
            {
                Thing rock = ThingMaker.MakeThing(rockDef);
                if (rock is Building_BasementExpansionRock expansionRock)
                {
                    expansionRock.linkedBasement = parent;
                    expansionRock.batchId = currentBatchId;
                }
                
                GenSpawn.Spawn(rock, cell, map);
                currentBatchRocks.Add(rock);
                
                // Add mining designation immediately
                map.designationManager.AddDesignation(new Designation(rock, DesignationDefOf.Mine));
            }
        }
        
        /// <summary>
        /// Called when a rock in the current batch is successfully mined.
        /// </summary>
        public void OnRockMined(Building_BasementExpansionRock rock)
        {
            if (!currentBatchRocks.Contains(rock))
                return;
            
            currentBatchRocks.Remove(rock);
            minedCountInBatch++;
            
            // Check if batch is complete (all 5 rocks mined)
            if (minedCountInBatch >= 5)
            {
                // Apply bonus space
                bonusSpace += 5;
                minedCountInBatch = 0;
                currentBatchRocks.Clear();
                
                // Notify player
                Messages.Message("SF_ExcavationComplete".Translate(5, TotalSpace, MaxSpace), 
                    parent, MessageTypeDefOf.PositiveEvent);
            }
        }
        
        /// <summary>
        /// Cancels the current excavation batch, removing all remaining rocks.
        /// Called when a mining designation is removed.
        /// </summary>
        public void CancelCurrentBatch()
        {
            if (!IsExcavationInProgress)
                return;
            
            // Destroy all remaining rocks in the batch
            List<Thing> rocksToDestroy = new List<Thing>(currentBatchRocks);
            foreach (Thing rock in rocksToDestroy)
            {
                if (rock != null && !rock.Destroyed)
                {
                    // Remove mining designation first if it exists
                    Designation des = rock.Map?.designationManager.DesignationOn(rock, DesignationDefOf.Mine);
                    if (des != null)
                    {
                        rock.Map.designationManager.RemoveDesignation(des);
                    }
                    rock.Destroy(DestroyMode.Vanish);
                }
            }
            
            currentBatchRocks.Clear();
            minedCountInBatch = 0;
            
            Messages.Message("SF_ExcavationCanceled".Translate(), parent, MessageTypeDefOf.NeutralEvent);
        }
        
        /// <summary>
        /// Checks if a rock belongs to this basement's current batch.
        /// </summary>
        public bool IsRockInCurrentBatch(Thing rock)
        {
            return currentBatchRocks.Contains(rock);
        }
        
        public override string CompInspectStringExtra()
        {
            string result = "SF_BasementSpace".Translate(TotalSpace, MaxSpace);
            
            if (IsExcavationInProgress)
            {
                int remaining = currentBatchRocks.Count;
                int mined = 5 - remaining;
                result += "\n" + "SF_ExcavationInProgress".Translate(mined, 5);
            }
            
            return result;
        }
    }
}
