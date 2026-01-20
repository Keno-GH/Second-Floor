using UnityEngine;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Mod settings for Second Floor.
    /// </summary>
    public class SecondFloorSettings : ModSettings
    {
        /// <summary>
        /// Number of tiles to mine per expansion batch for basements and mountain upstairs.
        /// </summary>
        public int tilesPerExpansion = 5;
        
        /// <summary>
        /// Minimum allowed value for tiles per expansion.
        /// </summary>
        public const int MinTilesPerExpansion = 5;
        
        /// <summary>
        /// Maximum allowed value for tiles per expansion.
        /// </summary>
        public const int MaxTilesPerExpansion = 20;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tilesPerExpansion, "tilesPerExpansion", 5);
            
            // Clamp value to valid range on load
            tilesPerExpansion = Mathf.Clamp(tilesPerExpansion, MinTilesPerExpansion, MaxTilesPerExpansion);
        }
    }
}
