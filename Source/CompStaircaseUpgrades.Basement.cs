using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Partial class for basement expansion functionality.
    /// Merged from CompBasementExpansion.
    /// </summary>
    public partial class CompStaircaseUpgrades
    {
        // =====================================================
        // Basement Expansion State (merged from CompBasementExpansion)
        // =====================================================
        
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
        /// When this reaches the configured tiles per expansion, the bonus is applied.
        /// </summary>
        private int minedCountInBatch = 0;
        
        /// <summary>
        /// Incrementing batch ID to identify rock groups.
        /// </summary>
        private int nextBatchId = 0;
        
        // =====================================================
        // Basement Expansion Properties
        // =====================================================
        
        /// <summary>
        /// Total space available (base + bonus). Only valid for basements.
        /// </summary>
        public int BasementTotalSpace => (ModExtension?.baseSpace ?? 0) + bonusSpace;
        
        /// <summary>
        /// Maximum possible space (base + max bonus). Only valid for basements.
        /// </summary>
        public int BasementMaxSpace => (ModExtension?.baseSpace ?? 0) + (ModExtension?.maxBonusSpace ?? 0);
        
        /// <summary>
        /// Current bonus space unlocked through mining.
        /// </summary>
        public int BonusSpace => bonusSpace;
        
        /// <summary>
        /// Whether an excavation batch is currently in progress.
        /// </summary>
        public bool IsExcavationInProgress => currentBatchRocks.Count > 0;
        
        /// <summary>
        /// Whether the basement has reached maximum expansion.
        /// </summary>
        public bool IsMaxExpansion => bonusSpace >= (ModExtension?.maxBonusSpace ?? 0);
        
        /// <summary>
        /// Number of rocks mined in the current batch.
        /// </summary>
        public int MinedCountInBatch => minedCountInBatch;
        
        // =====================================================
        // Mountain Upfloor Expansion Properties
        // =====================================================
        
        /// <summary>
        /// Total space available for mountain upfloors (base + bonus). Uses mountainBaseSpace from mod extension.
        /// </summary>
        public int MountainTotalSpace => (ModExtension?.mountainBaseSpace ?? 0) + bonusSpace;
        
        /// <summary>
        /// Maximum possible space for mountain upfloors. Dynamically calculated based on surrounding mountain roofs.
        /// </summary>
        public int MountainMaxSpace
        {
            get
            {
                Map map = parent.Map;
                if (map == null)
                    return ModExtension?.mountainBaseSpace ?? 0;
                
                int count = 0;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 10f, true))
                {
                    if (!cell.InBounds(map))
                        continue;
                    
                    RoofDef roof = cell.GetRoof(map);
                    if (roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
        
        /// <summary>
        /// Whether the mountain upfloor has reached maximum expansion (bonus space >= mountain max space - base space).
        /// </summary>
        public bool IsMountainMaxExpansion
        {
            get
            {
                if (ModExtension == null || !ModExtension.HasMountainExpansion)
                    return true;
                
                int maxBonusAvailable = MountainMaxSpace - (ModExtension?.mountainBaseSpace ?? 0);
                return bonusSpace >= maxBonusAvailable;
            }
        }
        
        /// <summary>
        /// Whether this staircase supports any expansion (basement or mountain).
        /// </summary>
        public bool HasAnyExpansion => (ModExtension?.HasBasementExpansion ?? false) || (ModExtension?.HasMountainExpansion ?? false);
        
        /// <summary>
        /// Gets the max space for display purposes (works for both basements and mountain upfloors).
        /// </summary>
        public int DisplayMaxSpace
        {
            get
            {
                if (ModExtension?.HasMountainExpansion == true)
                    return MountainMaxSpace;
                return BasementMaxSpace;
            }
        }
        
        /// <summary>
        /// Gets the total space for display purposes (works for both basements and mountain upfloors).
        /// </summary>
        public int DisplayTotalSpace
        {
            get
            {
                if (ModExtension?.HasMountainExpansion == true)
                    return MountainTotalSpace;
                return BasementTotalSpace;
            }
        }
        
        /// <summary>
        /// Whether expansion has reached maximum (works for both basements and mountain upfloors).
        /// </summary>
        public bool IsAnyMaxExpansion
        {
            get
            {
                if (ModExtension?.HasMountainExpansion == true)
                    return IsMountainMaxExpansion;
                return IsMaxExpansion;
            }
        }

        // =====================================================
        // Basement Expansion Methods
        // =====================================================
        
        /// <summary>
        /// Called during PostExposeData to save/load basement state.
        /// </summary>
        private void ExposeBasementData()
        {
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
        /// Spawns expansion rocks on free cells around the staircase with mining designations.
        /// The number of rocks is determined by the mod settings (tiles per expansion).
        /// Works for both basements and mountain upfloors.
        /// </summary>
        public void SpawnExpansionRocks()
        {
            if (IsExcavationInProgress || IsAnyMaxExpansion)
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
            
            // Find free cells around the staircase based on settings
            int tilesRequired = Main.Settings.tilesPerExpansion;
            List<IntVec3> freeCells = FindFreeCellsAroundStaircase(tilesRequired);
            
            if (freeCells.Count < tilesRequired)
            {
                Messages.Message("SF_NotEnoughFreeCells".Translate(freeCells.Count, tilesRequired), 
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
        /// Works for both basements and mountain upfloors.
        /// </summary>
        public void OnRockMined(Building_BasementExpansionRock rock)
        {
            if (!currentBatchRocks.Contains(rock))
                return;
            
            currentBatchRocks.Remove(rock);
            minedCountInBatch++;
            
            // Check if batch is complete (all rocks mined based on settings)
            int tilesRequired = Main.Settings.tilesPerExpansion;
            if (minedCountInBatch >= tilesRequired)
            {
                // Apply bonus space
                bonusSpace += tilesRequired;
                minedCountInBatch = 0;
                currentBatchRocks.Clear();
                
                // Notify player (use display properties that work for both basement and mountain)
                Messages.Message("SF_ExcavationComplete".Translate(tilesRequired, DisplayTotalSpace, DisplayMaxSpace), 
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
        
        /// <summary>
        /// Gets the expansion inspect string extra for basements and mountain upfloors.
        /// </summary>
        private string GetBasementInspectString()
        {
            if (ModExtension == null)
                return null;
            
            // Handle mountain upfloors with expansion
            if (ModExtension.HasMountainExpansion)
            {
                string result = "SF_MountainSpace".Translate(MountainTotalSpace, MountainMaxSpace);
                
                if (IsExcavationInProgress)
                {
                    int tilesRequired = Main.Settings.tilesPerExpansion;
                    int remaining = currentBatchRocks.Count;
                    int mined = tilesRequired - remaining;
                    result += "\n" + "SF_ExcavationInProgress".Translate(mined, tilesRequired);
                }
                
                return result;
            }
            
            // Handle basements with expansion
            if (ModExtension.HasBasementExpansion)
            {
                string result = "SF_BasementSpace".Translate(BasementTotalSpace, BasementMaxSpace);
                
                if (IsExcavationInProgress)
                {
                    int tilesRequired = Main.Settings.tilesPerExpansion;
                    int remaining = currentBatchRocks.Count;
                    int mined = tilesRequired - remaining;
                    result += "\n" + "SF_ExcavationInProgress".Translate(mined, tilesRequired);
                }
                
                return result;
            }
            
            return null;
        }
    }
}
