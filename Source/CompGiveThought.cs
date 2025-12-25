using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SecondFloor
{
    public class CompGiveThoughtStairs : ThingComp
    {
        public CompProperties_GiveThoughtStairs Props => (CompProperties_GiveThoughtStairs)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.def?.tickerType != TickerType.Normal) return;
            if ((Find.TickManager.TicksGame + parent.thingIDNumber) % 60 != 0) return;
            ApplyThought();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent.def?.tickerType != TickerType.Rare) return;
            ApplyThought();
        }

        public override void CompTickLong()
        {
            base.CompTickLong();
            if (parent.def?.tickerType != TickerType.Long) return;
            ApplyThought();
        }

        protected void ApplyThought()
        {
            if (parent is Building_Bed bed)
            {
                if (bed?.CurOccupants == null) return;
                if (bed?.GetComp<CompRefuelable>() != null && !bed.GetComp<CompRefuelable>().HasFuel) return; // Skip if bed is unfueled
                if (bed?.GetComp<CompPowerTrader>() != null && !bed.GetComp<CompPowerTrader>().PowerOn) return; // Skip if bed is unpowered

                ThoughtDef thoughtToGive = Props.thoughtDef;
                int impressivenessStage = 1; // Default to "dull" (stage 1)
                bool useNewSystem = false;
                
                var upgradesComp = parent.GetComp<CompStaircaseUpgrades>();
                if (upgradesComp != null)
                {
                    // Check for legacy thoughtReplacement first (backwards compatibility)
                    foreach (var upgrade in upgradesComp.GetActiveUpgradeDefs())
                    {
                        if (upgrade.thoughtReplacement != null)
                        {
                            thoughtToGive = upgrade.thoughtReplacement;
                            break;
                        }
                    }
                    
                    // Calculate impressiveness level from upgrades
                    int totalImpressivenessBonus = 0;
                    foreach (var upgrade in upgradesComp.GetActiveUpgradeDefs())
                    {
                        totalImpressivenessBonus += upgrade.impressivenessLevel;
                    }
                    impressivenessStage = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);
                    
                    // Use new system if we have impressiveness bonuses
                    if (totalImpressivenessBonus > 0)
                    {
                        useNewSystem = true;
                    }
                }
                
                // Try to use new impressiveness system
                if (useNewSystem)
                {
                    bool isBarracks = false;
                    bool isBasement = Props.thoughtDef?.defName == "SF_Low_Quality_Basement";
                    
                    // Check if barracks upgrade is installed
                    if (upgradesComp != null)
                    {
                        foreach (var upgrade in upgradesComp.GetActiveUpgradeDefs())
                        {
                            if (upgrade.defName == "SF_StaircaseUpgrade_Barracks")
                            {
                                isBarracks = true;
                                break;
                            }
                        }
                    }
                    
                    // Note: bedCount >= 2 means multiple private rooms, not barracks
                    // Only the barracks upgrade makes it barracks
                    
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
                foreach (var sleepingOccupant in bed.CurOccupants)
                {
                    sleepingOccupant.needs.mood.thoughts.memories.TryGainMemory(thoughtToGive);
                }
            }
        }

    }
}
