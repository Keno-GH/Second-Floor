using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Properties for the basement expansion component.
    /// Configures the base space and maximum bonus space for basements.
    /// </summary>
    public class CompProperties_BasementExpansion : CompProperties
    {
        /// <summary>
        /// The base space available in the basement before any expansion.
        /// </summary>
        public int baseSpace = 30;
        
        /// <summary>
        /// The maximum bonus space that can be unlocked through mining.
        /// Total max space = baseSpace + maxBonusSpace.
        /// </summary>
        public int maxBonusSpace = 270;
        
        public CompProperties_BasementExpansion()
        {
            this.compClass = typeof(CompBasementExpansion);
        }
    }
}
