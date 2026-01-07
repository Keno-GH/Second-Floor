using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;
using HarmonyLib;
using System.Reflection;

namespace SecondFloor
{
    class Main : Mod
    {
        public Main(ModContentPack content) : base(content)
        {
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Second Floor";
        }
    }
    
    /// <summary>
    /// Static constructor to initialize mod integrations after all mods are loaded.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModInitializer
    {
        static ModInitializer()
        {
            // Initialize DBH integration if available
            DBHReflectionHelper.Initialize();
            
            // Hide upgrade building designators from the architect menu
            // We need the designationCategory for blueprint/frame generation, but we don't want
            // players to build these directly - they're placed via the staircase ITab
            HideUpgradeBuildingDesignators();
        }
        
        private static void HideUpgradeBuildingDesignators()
        {
            var miscCategory = DefDatabase<DesignationCategoryDef>.GetNamed("Misc", errorOnFail: false);
            if (miscCategory == null)
            {
                Log.Warning("[SecondFloor] Could not find Misc designation category to hide upgrade building designators.");
                return;
            }
            
            // Get the resolved designators list
            var resolvedDesignators = miscCategory.AllResolvedDesignators;
            
            // Find and remove designators for our upgrade buildings
            var designatorsToRemove = resolvedDesignators
                .OfType<Designator_Build>()
                .Where(d => d.PlacingDef?.defName?.StartsWith("SF_UpgradeBuilding_") == true)
                .ToList();
            
            foreach (var designator in designatorsToRemove)
            {
                miscCategory.AllResolvedDesignators.Remove(designator);
            }
            
            if (designatorsToRemove.Count > 0)
            {
                Log.Message($"[SecondFloor] Hidden {designatorsToRemove.Count} upgrade building designators from architect menu.");
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {

    }
}
