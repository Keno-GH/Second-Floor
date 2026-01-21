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
        
        /// <summary>
        /// If true, this upstairs staircase counts mountain roofs (thick and thin rock) instead of constructed roofs.
        /// Used for cave base staircases that build into the mountain overhead.
        /// </summary>
        public bool isMountainUpfloor = false;
        
        /// <summary>
        /// If true, this staircase uses gravship substructures to count available space instead of constructed roofs.
        /// Used for staircases built on gravships.
        /// </summary>
        public bool isGravshipStaircase = false;
        
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
        
        // =====================================================
        // Mountain Upfloor Expansion
        // =====================================================
        
        /// <summary>
        /// The base space available in the mountain upfloor before any expansion.
        /// Only used when isMountainUpfloor is true. Max space is determined by surrounding mountain roofs.
        /// </summary>
        public int mountainBaseSpace = 0;
        
        /// <summary>
        /// Whether this mountain upfloor supports expansion (has mountainBaseSpace > 0).
        /// Mountain upfloors use mining to expand space up to the dynamic max determined by surrounding mountain roofs.
        /// </summary>
        public bool HasMountainExpansion => isMountainUpfloor && mountainBaseSpace > 0;
        
        // =====================================================
        // Gravship Staircase Configuration
        // =====================================================
        
        /// <summary>
        /// The base space available in the gravship staircase.
        /// Only used when isGravshipStaircase is true. Max space is determined by surrounding gravship substructures.
        /// </summary>
        public int gravshipBaseSpace = 0;
        
        /// <summary>
        /// Whether this gravship staircase supports dynamic space (has gravshipBaseSpace > 0).
        /// Gravship staircases do not use mining - space is determined by surrounding substructures.
        /// </summary>
        public bool HasGravshipSpace => isGravshipStaircase && gravshipBaseSpace > 0;
    }
}