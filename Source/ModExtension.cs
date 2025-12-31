using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
    
    public class SecondFloorModExtension : DefModExtension
    {
        /// <summary>
        /// The floor level this staircase leads to. Used for distance separation rules.
        /// </summary>
        public StaircaseFloorLevel floorLevel = StaircaseFloorLevel.Upstairs;
        public bool RemoveSoakingWet = false;
        public bool RemoveSleptOutside = false;
        public bool RemoveSleptInCold = false;
        public bool RemoveSleptInHeat = false;
        public bool RemoveSleptInBarracks = false;
        public bool RemoveSleepDisturbed = false;
        // public bool RemoveRemoveToxicFallout = false;
        public bool RemoveSharedBed = false;
        public bool RemoveSleptInBedroom = true;
        //public bool RemoveSunlightSensitivity_Mild = false;
        public bool ideologySecondFloorAssignmentAllowed = false;
        public bool RemoveGreedyWant = false;
        public HediffDef customHediff = null;
    }
}