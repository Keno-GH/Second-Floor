using RimWorld;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Defines the floor level type for staircases.
    /// Used to enforce minimum distance between same-type staircases.
    /// </summary>
    public enum StaircaseFloorLevel
    {
        Upstairs,
        Basement
    }
    
    /// <summary>
    /// Comprehensive mod extension for staircase configuration.
    /// Contains all static configuration for staircase behavior, thoughts, beds, and basement expansion.
    /// </summary>
    public class SecondFloorModExtension : DefModExtension
    {
        // =====================================================
        // Floor Level Configuration
        // =====================================================
        
        /// <summary>
        /// The floor level this staircase leads to. Used for distance separation rules.
        /// </summary>
        public StaircaseFloorLevel floorLevel = StaircaseFloorLevel.Upstairs;
        
        // =====================================================
        // Thought Removal Flags
        // =====================================================
        
        public bool RemoveSoakingWet = false;
        public bool RemoveSleptOutside = false;
        public bool RemoveSleptInCold = false;
        public bool RemoveSleptInHeat = false;
        public bool RemoveSleptInBarracks = false;
        public bool RemoveSleepDisturbed = false;
        public bool RemoveSharedBed = false;
        public bool RemoveSleptInBedroom = true;
        public bool ideologySecondFloorAssignmentAllowed = false;
        public bool RemoveGreedyWant = false;
        
        // =====================================================
        // Thought Application (merged from CompProperties_GiveThoughtStairs)
        // =====================================================
        
        /// <summary>
        /// The default thought to give sleeping pawns. Used as fallback if impressiveness system doesn't apply.
        /// </summary>
        public ThoughtDef thoughtDef;
        
        // =====================================================
        // Bed Count (merged from CompProperties_MultipleBeds)
        // =====================================================
        
        /// <summary>
        /// Base number of bed slots in this staircase. Can be modified by upgrades.
        /// </summary>
        public int bedCount = 1;
        
        // =====================================================
        // Basement Expansion (merged from CompProperties_BasementExpansion)
        // =====================================================
        
        /// <summary>
        /// The base space available in the basement before any expansion.
        /// Only used when floorLevel is Basement.
        /// </summary>
        public int baseSpace = 0;
        
        /// <summary>
        /// The maximum bonus space that can be unlocked through mining.
        /// Total max space = baseSpace + maxBonusSpace.
        /// Only used when floorLevel is Basement.
        /// </summary>
        public int maxBonusSpace = 0;
        
        /// <summary>
        /// Whether this staircase supports basement expansion (has baseSpace > 0).
        /// </summary>
        public bool HasBasementExpansion => baseSpace > 0;
    }
}