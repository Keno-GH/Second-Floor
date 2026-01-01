using RimWorld;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    /// <summary>
    /// Partial class for thought application functionality.
    /// Merged from CompGiveThoughtStairs.
    /// </summary>
    public partial class CompStaircaseUpgrades
    {
        // =====================================================
        // Thought Application (merged from CompGiveThoughtStairs)
        // =====================================================
        
        /// <summary>
        /// Called every 60 ticks to apply thoughts to sleeping pawns.
        /// </summary>
        private void TickThoughtApplication()
        {
            // Only run every 60 ticks, offset by thingID to spread load
            if ((Find.TickManager.TicksGame + parent.thingIDNumber) % 60 != 0)
                return;
            
            ApplyThought();
        }
        
        /// <summary>
        /// Applies the appropriate thought to sleeping pawns based on upgrades and impressiveness.
        /// </summary>
        protected void ApplyThought()
        {
            if (!(parent is Building_Bed bed))
                return;
            
            if (bed.CurOccupants == null)
                return;
            
            // Always apply darkness thought check regardless of fuel/power status
            // (if there's no fuel for lighting, that means it's dark!)
            ApplyDarknessThought(bed);
            
            // For the main room quality thoughts, check fuel/power requirements
            // These are only needed if upgrades that consume fuel/power are active
            bool hasFuelRequiringUpgrade = false;
            bool hasPowerRequiringUpgrade = false;
            foreach (var upgrade in GetConstructedUpgradeDefs())
            {
                if (upgrade.fuelPerBed > 0) hasFuelRequiringUpgrade = true;
                if (upgrade.requiresPower) hasPowerRequiringUpgrade = true;
            }
            
            // Only check fuel if we have fuel-consuming upgrades
            if (hasFuelRequiringUpgrade)
            {
                var refuelable = bed.GetComp<CompRefuelable>();
                if (refuelable != null && !refuelable.HasFuel) return;
            }
            
            // Only check power if we have power-consuming upgrades  
            if (hasPowerRequiringUpgrade)
            {
                var power = bed.GetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn) return;
            }

            ThoughtDef thoughtToGive = ModExtension?.thoughtDef;
            int impressivenessStage = 1; // Default to "dull" (stage 1)
            bool useNewSystem = false;
            
            // Check for legacy thoughtReplacement first (backwards compatibility)
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.thoughtReplacement != null)
                {
                    thoughtToGive = upgrade.thoughtReplacement;
                    break;
                }
            }
            
            // Calculate impressiveness level from upgrades
            int totalImpressivenessBonus = 0;
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                totalImpressivenessBonus += upgrade.impressivenessLevel;
            }
            impressivenessStage = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);
            
            // Use new system if we have impressiveness bonuses
            if (totalImpressivenessBonus > 0)
            {
                useNewSystem = true;
            }
            
            // Try to use new impressiveness system
            if (useNewSystem)
            {
                bool isBarracks = IsBarracks;
                bool isBasement = ModExtension?.floorLevel == StaircaseFloorLevel.Basement;
                
                // Get the appropriate new thought
                ThoughtDef newThought = null;
                if (isBasement)
                {
                    newThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInBasement");
                }
                else if (isBarracks)
                {
                    newThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInSecondFloorBarracks");
                }
                else
                {
                    newThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInSecondFloorRoom");
                }
                
                // Only switch to new system if we found the thought
                if (newThought != null)
                {
                    thoughtToGive = newThought;
                    
                    // Apply with stage
                    foreach (var sleepingOccupant in bed.CurOccupants)
                    {
                        var memory = (Thought_Memory)ThoughtMaker.MakeThought(thoughtToGive);
                        if (memory != null)
                        {
                            memory.SetForcedStage(impressivenessStage);
                            sleepingOccupant.needs.mood.thoughts.memories.TryGainMemory(memory);
                        }
                    }
                    return;
                }
            }
            
            // Fallback to old system
            if (thoughtToGive != null)
            {
                foreach (var sleepingOccupant in bed.CurOccupants)
                {
                    sleepingOccupant.needs.mood.thoughts.memories.TryGainMemory(thoughtToGive);
                }
            }
        }
        
        /// <summary>
        /// Applies the "slept in the dark" thought to sleeping pawns if no lighting upgrades are active.
        /// Checks against EnvironmentDark's nullifying traits and genes to exclude pawns with dark vision.
        /// </summary>
        private void ApplyDarknessThought(Building_Bed bed)
        {
            // Check if any lighting upgrade is active
            foreach (var upgrade in GetActiveUpgradeDefs())
            {
                if (upgrade.removesSleptInDark)
                    return; // Has lighting, no darkness thought
            }
            
            // Get the darkness thoughts
            ThoughtDef sleptInDarkThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SF_SleptInTheDark");
            if (sleptInDarkThought == null)
                return;
                
            ThoughtDef environmentDarkThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("EnvironmentDark");
            
            // Apply to each sleeping occupant
            foreach (var sleepingOccupant in bed.CurOccupants)
            {
                if (sleepingOccupant?.needs?.mood?.thoughts?.memories == null)
                    continue;
                    
                // Check if pawn is excluded by EnvironmentDark's nullifying conditions
                if (environmentDarkThought != null)
                {
                    // Check nullifying traits
                    if (environmentDarkThought.nullifyingTraits != null && sleepingOccupant.story?.traits != null)
                    {
                        bool hasNullifyingTrait = false;
                        foreach (var traitDef in environmentDarkThought.nullifyingTraits)
                        {
                            if (sleepingOccupant.story.traits.HasTrait(traitDef))
                            {
                                hasNullifyingTrait = true;
                                break;
                            }
                        }
                        if (hasNullifyingTrait)
                            continue;
                    }
                    
                    // Check nullifying genes (Biotech)
                    if (environmentDarkThought.nullifyingGenes != null && sleepingOccupant.genes != null)
                    {
                        bool hasNullifyingGene = false;
                        foreach (var geneDef in environmentDarkThought.nullifyingGenes)
                        {
                            if (sleepingOccupant.genes.HasActiveGene(geneDef))
                            {
                                hasNullifyingGene = true;
                                break;
                            }
                        }
                        if (hasNullifyingGene)
                            continue;
                    }
                }
                
                sleepingOccupant.needs.mood.thoughts.memories.TryGainMemory(sleptInDarkThought);
            }
        }
    }
}
