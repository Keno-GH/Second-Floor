using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;

namespace SecondFloor
{
    /// <summary>
    /// Reflection helper for Dubs Bad Hygiene integration.
    /// Uses reflection to access DBH types and methods since the source is closed.
    /// Supports: Full DBH (dubwise.dubsbadhygiene), Lite (dubwise.dubsbadhygiene.lite), 
    /// and Thirst addon (dubwise.dubsbadhygiene.thirst).
    /// </summary>
    public static class DBHReflectionHelper
    {
        // DBH mod detection
        private static bool? _isDBHLoaded;
        private static bool? _isDBHLiteLoaded;
        private static bool? _isDBHThirstLoaded;
        
        // Cached types
        private static Type _needHygieneType;
        private static Type _needBladderType;
        private static Type _needThirstType;
        private static Type _compPipeType;
        private static Type _plumbingNetType;
        private static Type _mapCompType; // DBH's MapComponent for plumbing
        
        // Cached methods/properties
        private static PropertyInfo _pipeNetProperty; // CompPipe.pipeNet
        private static PropertyInfo _waterStorageProperty; // PlumbingNet.WaterStorage or similar
        private static PropertyInfo _sewageStorageProperty;
        private static PropertyInfo _hotWaterStorageProperty;
        private static MethodInfo _consumeWaterMethod;
        private static MethodInfo _pushSewageMethod;
        private static MethodInfo _consumeHotWaterMethod;
        
        // ContaminationLevel type for PullWater out parameter
        private static Type _contaminationLevelType;
        
        // NeedDef names
        public const string HygieneNeedDefName = "Hygiene";
        public const string BladderNeedDefName = "Bladder";
        public const string ThirstNeedDefName = "DBHThirst";
        
        // Water/sewage amounts (realistic values)
        public const float ToiletWaterUse = 14f;
        public const float ToiletSewageOutput = 14f;
        public const float ShowerWaterUse = 65f;
        public const float ShowerSewageOutput = 0f; // Showers drain to floor, not sewage
        public const float BasinWaterUse = 3f;
        public const float BasinSewageOutput = 0f;
        public const float DrinkWaterUse = 1f;
        
        private static bool _initialized = false;
        
        /// <summary>
        /// Returns true if any version of DBH is loaded.
        /// </summary>
        public static bool IsDBHActive => IsDBHLoaded || IsDBHLiteLoaded;
        
        /// <summary>
        /// Returns true if thirst need is available (full DBH with thirst enabled, or thirst addon).
        /// </summary>
        public static bool IsThirstAvailable => IsDBHLoaded || IsDBHThirstLoaded;
        
        public static bool IsDBHLoaded
        {
            get
            {
                if (!_isDBHLoaded.HasValue)
                {
                    _isDBHLoaded = ModsConfig.IsActive("dubwise.dubsbadhygiene");
                }
                return _isDBHLoaded.Value;
            }
        }
        
        public static bool IsDBHLiteLoaded
        {
            get
            {
                if (!_isDBHLiteLoaded.HasValue)
                {
                    _isDBHLiteLoaded = ModsConfig.IsActive("dubwise.dubsbadhygiene.lite");
                }
                return _isDBHLiteLoaded.Value;
            }
        }
        
        public static bool IsDBHThirstLoaded
        {
            get
            {
                if (!_isDBHThirstLoaded.HasValue)
                {
                    _isDBHThirstLoaded = ModsConfig.IsActive("dubwise.dubsbadhygiene.thirst");
                }
                return _isDBHThirstLoaded.Value;
            }
        }
        
        /// <summary>
        /// Initialize reflection cache. Call once at startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized || !IsDBHActive)
                return;
                
            try
            {
                // Find DBH assembly
                Assembly dbhAssembly = null;
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    if (mod.PackageId.ToLower() == "dubwise.dubsbadhygiene" || 
                        mod.PackageId.ToLower() == "dubwise.dubsbadhygiene.lite")
                    {
                        foreach (var assembly in mod.assemblies.loadedAssemblies)
                        {
                            if (assembly.GetType("DubsBadHygiene.Need_Hygiene") != null)
                            {
                                dbhAssembly = assembly;
                                break;
                            }
                        }
                        if (dbhAssembly != null) break;
                    }
                }
                
                if (dbhAssembly == null)
                {
                    Log.Warning("[SecondFloor] DBH detected but assembly not found");
                    return;
                }
                
                // Cache types
                _needHygieneType = dbhAssembly.GetType("DubsBadHygiene.Need_Hygiene");
                _needBladderType = dbhAssembly.GetType("DubsBadHygiene.Need_Bladder");
                _needThirstType = dbhAssembly.GetType("DubsBadHygiene.Need_Thirst");
                _compPipeType = dbhAssembly.GetType("DubsBadHygiene.CompPipe");
                _plumbingNetType = dbhAssembly.GetType("DubsBadHygiene.PlumbingNet");
                _mapCompType = dbhAssembly.GetType("DubsBadHygiene.MapComponent_Hygiene");
                _contaminationLevelType = dbhAssembly.GetType("DubsBadHygiene.ContaminationLevel");
                
                // Cache CompPipe.pipeNet property
                if (_compPipeType != null)
                {
                    _pipeNetProperty = _compPipeType.GetProperty("pipeNet", BindingFlags.Public | BindingFlags.Instance);
                }
                
                // Cache PlumbingNet properties and methods
                if (_plumbingNetType != null)
                {
                    _waterStorageProperty = _plumbingNetType.GetProperty("WaterStorage", BindingFlags.Public | BindingFlags.Instance);
                    _sewageStorageProperty = _plumbingNetType.GetProperty("SewageStorage", BindingFlags.Public | BindingFlags.Instance);
                    _hotWaterStorageProperty = _plumbingNetType.GetProperty("HotWaterStorage", BindingFlags.Public | BindingFlags.Instance);
                    
                    _consumeWaterMethod = _plumbingNetType.GetMethod("PullWater", BindingFlags.Public | BindingFlags.Instance);
                    _pushSewageMethod = _plumbingNetType.GetMethod("PushSewage", BindingFlags.Public | BindingFlags.Instance);
                    _consumeHotWaterMethod = _plumbingNetType.GetMethod("PullHotWater", BindingFlags.Public | BindingFlags.Instance);
                }
                
                _initialized = true;
                Log.Message($"[SecondFloor] DBH integration initialized. Hygiene: {_needHygieneType != null}, Bladder: {_needBladderType != null}, Thirst: {_needThirstType != null}, Pipes: {_compPipeType != null}");
            }
            catch (Exception ex)
            {
                Log.Error($"[SecondFloor] Failed to initialize DBH reflection: {ex}");
            }
        }
        
        /// <summary>
        /// Gets the hygiene need from a pawn, or null if not available.
        /// </summary>
        public static Need GetHygieneNeed(Pawn pawn)
        {
            if (!IsDBHActive || pawn?.needs == null)
                return null;
            return pawn.needs.TryGetNeed(DefDatabase<NeedDef>.GetNamedSilentFail(HygieneNeedDefName));
        }
        
        /// <summary>
        /// Gets the bladder need from a pawn, or null if not available.
        /// </summary>
        public static Need GetBladderNeed(Pawn pawn)
        {
            if (!IsDBHActive || pawn?.needs == null)
                return null;
            return pawn.needs.TryGetNeed(DefDatabase<NeedDef>.GetNamedSilentFail(BladderNeedDefName));
        }
        
        /// <summary>
        /// Gets the thirst need from a pawn, or null if not available.
        /// </summary>
        public static Need GetThirstNeed(Pawn pawn)
        {
            if (!IsThirstAvailable || pawn?.needs == null)
                return null;
            return pawn.needs.TryGetNeed(DefDatabase<NeedDef>.GetNamedSilentFail(ThirstNeedDefName));
        }
        
        /// <summary>
        /// Gets the CompPipe from a thing, or null if not available.
        /// </summary>
        public static object GetCompPipe(Thing thing)
        {
            if (_compPipeType == null || thing == null)
                return null;
                
            var comps = (thing as ThingWithComps)?.AllComps;
            if (comps == null)
                return null;
                
            foreach (var comp in comps)
            {
                if (_compPipeType.IsInstanceOfType(comp))
                    return comp;
            }
            return null;
        }
        
        /// <summary>
        /// Gets the PlumbingNet from a CompPipe.
        /// </summary>
        public static object GetPlumbingNet(object compPipe)
        {
            if (compPipe == null || _pipeNetProperty == null)
                return null;
            return _pipeNetProperty.GetValue(compPipe);
        }
        
        /// <summary>
        /// Gets the water storage amount from a PlumbingNet.
        /// </summary>
        public static float GetWaterStorage(object plumbingNet)
        {
            if (plumbingNet == null || _waterStorageProperty == null)
                return 0f;
            try
            {
                return (float)_waterStorageProperty.GetValue(plumbingNet);
            }
            catch
            {
                return 0f;
            }
        }
        
        /// <summary>
        /// Gets the hot water storage amount from a PlumbingNet.
        /// </summary>
        public static float GetHotWaterStorage(object plumbingNet)
        {
            if (plumbingNet == null || _hotWaterStorageProperty == null)
                return 0f;
            try
            {
                return (float)_hotWaterStorageProperty.GetValue(plumbingNet);
            }
            catch
            {
                return 0f;
            }
        }
        
        /// <summary>
        /// Attempts to pull water from the plumbing net.
        /// Returns true if successful.
        /// </summary>
        public static bool TryPullWater(object plumbingNet, float amount)
        {
            if (plumbingNet == null)
                return false;
                
            // Check if enough water is available
            float available = GetWaterStorage(plumbingNet);
            if (available < amount)
                return false;
            
            // Try to consume water - PullWater(float waterUsed, out ContaminationLevel contam)
            if (_consumeWaterMethod != null && _contaminationLevelType != null)
            {
                try
                {
                    // Create the out parameter - default enum value (0)
                    object contamOut = Enum.ToObject(_contaminationLevelType, 0);
                    object[] args = new object[] { amount, contamOut };
                    _consumeWaterMethod.Invoke(plumbingNet, args);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[SecondFloor] TryPullWater failed: {ex.Message}");
                }
            }
            
            // Fallback: try to set the property directly (subtract amount)
            if (_waterStorageProperty?.CanWrite == true)
            {
                try
                {
                    _waterStorageProperty.SetValue(plumbingNet, available - amount);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Attempts to pull hot water from the plumbing net.
        /// Returns true if successful.
        /// </summary>
        public static bool TryPullHotWater(object plumbingNet, float amount)
        {
            if (plumbingNet == null)
                return false;
                
            float available = GetHotWaterStorage(plumbingNet);
            if (available < amount)
                return false;
            
            if (_consumeHotWaterMethod != null)
            {
                try
                {
                    _consumeHotWaterMethod.Invoke(plumbingNet, new object[] { amount });
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            if (_hotWaterStorageProperty?.CanWrite == true)
            {
                try
                {
                    _hotWaterStorageProperty.SetValue(plumbingNet, available - amount);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Attempts to push sewage to the plumbing net.
        /// Returns true if successful.
        /// </summary>
        public static bool TryPushSewage(object plumbingNet, float amount)
        {
            if (plumbingNet == null || amount <= 0)
                return true; // No sewage to push is a success
                
            if (_pushSewageMethod != null)
            {
                try
                {
                    _pushSewageMethod.Invoke(plumbingNet, new object[] { amount });
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return true; // If no method, assume sewage is handled elsewhere
        }
        
        /// <summary>
        /// Checks if a plumbing net has enough water for the specified amount.
        /// </summary>
        public static bool HasEnoughWater(object plumbingNet, float amount)
        {
            return GetWaterStorage(plumbingNet) >= amount;
        }
        
        /// <summary>
        /// Checks if a plumbing net has enough hot water for the specified amount.
        /// </summary>
        public static bool HasEnoughHotWater(object plumbingNet, float amount)
        {
            return GetHotWaterStorage(plumbingNet) >= amount;
        }
        
        /// <summary>
        /// Sets a pawn's need to a specific level (0-1).
        /// </summary>
        public static void SetNeedLevel(Need need, float level)
        {
            if (need == null)
                return;
            need.CurLevel = level;
        }
        
        /// <summary>
        /// Restores a need by a certain amount, optionally capped at a maximum.
        /// </summary>
        public static void RestoreNeed(Need need, float amount, float maxLevel = 1f)
        {
            if (need == null)
                return;
            float newLevel = need.CurLevel + amount;
            need.CurLevel = Math.Min(newLevel, maxLevel);
        }
        
        /// <summary>
        /// Sets a need to a target level if current level is below it.
        /// </summary>
        public static void RestoreNeedToLevel(Need need, float targetLevel)
        {
            if (need == null || need.CurLevel >= targetLevel)
                return;
            need.CurLevel = targetLevel;
        }
    }
}
