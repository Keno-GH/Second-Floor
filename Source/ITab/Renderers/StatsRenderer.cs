using Verse;
using RimWorld;
using UnityEngine;
using System.Linq;

namespace SecondFloor
{
    /// <summary>
    /// Renders the stats header panel for the staircase ITab.
    /// Stats displayed change based on the currently selected tab.
    /// </summary>
    public static class StatsRenderer
    {
        /// <summary>
        /// Draws the stats header panel. Content varies based on the selected tab.
        /// </summary>
        public static void DrawStatsHeader(Rect rect, Thing staircase, CompStaircaseUpgrades comp, UpgradeTabType currentTab)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            
            switch (currentTab)
            {
                case UpgradeTabType.Manage:
                    DrawManageStats(listing, staircase, comp);
                    break;
                case UpgradeTabType.Control:
                    DrawControlStats(listing, staircase, comp);
                    break;
                case UpgradeTabType.Construction:
                    DrawConstructionStats(listing, staircase, comp);
                    break;
            }
            
            listing.End();
        }
        
        /// <summary>
        /// Calculates the required height for the stats header.
        /// Returns a consistent maximum height across all tabs to prevent layout shifting.
        /// </summary>
        public static float CalculateStatsHeight(CompStaircaseUpgrades comp, UpgradeTabType currentTab)
        {
            // Use consistent height across all tabs to prevent layout shifting when switching tabs
            // Maximum stats: 7 lines + 1 slider + 1 hygiene line + 1 noise line = ~10 lines worth
            // Base: 7 stat lines, 1 slider, 1 optional hygiene, 1 noise = consistent layout
            float maxHeight = TabLayout.StatsLineHeight * 8 + TabLayout.SliderHeight;
            
            return maxHeight;
        }
        
        /// <summary>
        /// Stats for Manage tab: bedspaces, temperature, impressiveness, comfort, rest effectiveness, noise.
        /// </summary>
        private static void DrawManageStats(Listing_Standard listing, Thing staircase, CompStaircaseUpgrades comp)
        {
            // Bed spaces
            listing.Label("SF_Stat_BedSpaces".Translate(comp.BedCount));
            
            // Room type
            string roomType = GetRoomTypeLabel(staircase, comp);
            listing.Label("SF_Stat_RoomType".Translate(roomType));
            
            // Impressiveness
            string impressivenessLabel = GetImpressivenessLabel(comp);
            listing.Label("SF_Stat_Impressiveness".Translate(impressivenessLabel));
            
            // Comfort
            float comfort = GetTotalComfort(staircase, comp);
            listing.Label("SF_Stat_Comfort".Translate(comfort.ToStringPercent()));
            
            // Rest effectiveness
            float restEffectiveness = GetTotalRestEffectiveness(staircase, comp);
            listing.Label("SF_Stat_RestEffectiveness".Translate(restEffectiveness.ToStringPercent()));
            
            // Temperature
            DrawTemperatureDisplay(listing, staircase, comp, true);
            
            // Noise protection
            bool noiseProtected = UpgradeFiltering.HasNoiseProtection(comp);
            string noiseLabel = noiseProtected ? "SF_Stat_NoiseProtected".Translate() : "SF_Stat_NoiseExposed".Translate();
            GUI.color = noiseProtected ? Color.green : new Color(1f, 0.6f, 0.2f);
            listing.Label("SF_Stat_NoiseProtection".Translate(noiseLabel));
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Stats for Control tab: power, fuel, current/target temp, battery, noise.
        /// </summary>
        private static void DrawControlStats(Listing_Standard listing, Thing staircase, CompStaircaseUpgrades comp)
        {
            // Power consumption
            float powerUsage = comp.CurrentPowerConsumption;
            bool hasPower = comp.HasPower();
            string powerLabel;
            
            // Show range for upgrades with dynamic power (like Sleep Accelerator)
            if (comp.HasAnyDynamicPowerUpgrade())
            {
                var (minPower, maxPower) = comp.GetPowerConsumptionRange();
                powerLabel = $"{minPower:F0}W to {maxPower:F0}W ({powerUsage:F0}W)";
            }
            else
            {
                powerLabel = $"{powerUsage:F0}W";
            }
            
            if (!hasPower && comp.HasAnyPowerRequiringUpgrade())
            {
                powerLabel += " " + "SF_NoPower".Translate();
                GUI.color = Color.red;
            }
            listing.Label("SF_Stat_PowerUsage".Translate(powerLabel));
            GUI.color = Color.white;
            
            // Fuel consumption with throttle percentage for controllable fueled changers
            float fuelUsage = comp.CurrentFuelConsumption;
            bool hasFuel = comp.HasFuel();
            string fuelLabel = $"{fuelUsage:F1}/day";
            
            // Show throttle percentage if there are controllable fueled temp changers
            if (comp.HasAnyControllableFueledTempChanger())
            {
                float utilizationRatio = comp.GetFueledUtilizationRatio();
                int throttlePercent = Mathf.RoundToInt(utilizationRatio * 100f);
                fuelLabel += $" ({throttlePercent}%)";
            }
            
            if (!hasFuel && comp.HasAnyFuelRequiringUpgrade())
            {
                fuelLabel += " " + "SF_NoFuel".Translate();
                GUI.color = Color.red;
            }
            listing.Label("SF_Stat_FuelUsage".Translate(fuelLabel));
            GUI.color = Color.white;
            
            // Current temperature
            float currentTemp = comp.CurrentVirtualTemperature;
            listing.Label("SF_Stat_CurrentTemp".Translate(currentTemp.ToStringTemperature("F0")));
            
            // Target temperature (if has smart temp modifiers OR controllable fueled changers)
            if (comp.HasAnySmartTempModifier() || comp.HasAnyControllableFueledTempChanger())
            {
                Rect sliderRect = listing.GetRect(TabLayout.SliderHeight);
                Rect labelRect = new Rect(sliderRect.x, sliderRect.y, 120f, sliderRect.height);
                Rect actualSliderRect = new Rect(labelRect.xMax, sliderRect.y, sliderRect.width - 120f, sliderRect.height);
                
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, "SF_TargetTemp".Translate() + ": " + comp.targetTemperature.ToStringTemperature("F0"));
                Text.Anchor = TextAnchor.UpperLeft;
                
                float newTarget = Widgets.HorizontalSlider(actualSliderRect, comp.targetTemperature, -10f, 40f, true, null, "SF_TempMin".Translate(), "SF_TempMax".Translate(), 1f);
                if (newTarget != comp.targetTemperature)
                {
                    comp.targetTemperature = newTarget;
                }
            }
            else
            {
                listing.Label("SF_Stat_TargetTemp".Translate("N/A"));
            }
            
            // Battery storage
            if (comp.HasBatteryStorage())
            {
                float storedEnergy = comp.StoredEnergy;
                float maxCapacity = comp.GetTotalBatteryCapacity();
                listing.Label("SF_Stat_BatteryStored".Translate($"{storedEnergy:F0} / {maxCapacity:F0} Wd"));
            }
            
            // Noise protection
            bool noiseProtected = UpgradeFiltering.HasNoiseProtection(comp);
            string noiseLabel = noiseProtected ? "SF_Stat_NoiseProtected".Translate() : "SF_Stat_NoiseExposed".Translate();
            GUI.color = noiseProtected ? Color.green : new Color(1f, 0.6f, 0.2f);
            listing.Label("SF_Stat_NoiseProtection".Translate(noiseLabel));
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Stats for Construction tab: space, impressiveness, comfort, rest effectiveness, beds, temp, hygiene, noise.
        /// </summary>
        private static void DrawConstructionStats(Listing_Standard listing, Thing staircase, CompStaircaseUpgrades comp)
        {
            // Space used/available
            float totalSpace = comp.GetTotalSpace();
            float usedSpace = comp.GetUsedSpace();
            float availableSpace = totalSpace - usedSpace;
            listing.Label("SF_Stat_Space".Translate($"{usedSpace:F0}/{totalSpace:F0}", $"{availableSpace:F0}"));
            
            // Impressiveness
            string impressivenessLabel = GetImpressivenessLabel(comp);
            listing.Label("SF_Stat_Impressiveness".Translate(impressivenessLabel));
            
            // Comfort
            float comfort = GetTotalComfort(staircase, comp);
            listing.Label("SF_Stat_Comfort".Translate(comfort.ToStringPercent()));
            
            // Rest effectiveness
            float restEffectiveness = GetTotalRestEffectiveness(staircase, comp);
            listing.Label("SF_Stat_RestEffectiveness".Translate(restEffectiveness.ToStringPercent()));
            
            // Bed count
            listing.Label("SF_Stat_BedSpaces".Translate(comp.BedCount));
            
            // Temperature (simple display, no slider)
            float currentTemp = comp.CurrentVirtualTemperature;
            listing.Label("SF_Stat_Temperature".Translate(currentTemp.ToStringTemperature("F0")));
            
            // Hygiene capacity (if any bathroom upgrades)
            float hygieneCapacity = UpgradeFiltering.GetMaxHygieneCapacity(comp);
            if (hygieneCapacity > 0)
            {
                listing.Label("SF_Stat_HygieneCapacity".Translate(hygieneCapacity.ToStringPercent()));
            }
            
            // Noise protection
            bool noiseProtected = UpgradeFiltering.HasNoiseProtection(comp);
            string noiseLabel = noiseProtected ? "SF_Stat_NoiseProtected".Translate() : "SF_Stat_NoiseExposed".Translate();
            GUI.color = noiseProtected ? Color.green : new Color(1f, 0.6f, 0.2f);
            listing.Label("SF_Stat_NoiseProtection".Translate(noiseLabel));
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Helper to draw temperature display with breakdown.
        /// </summary>
        private static void DrawTemperatureDisplay(Listing_Standard listing, Thing staircase, CompStaircaseUpgrades comp, bool showSlider)
        {
            float currentTemp = comp.CurrentVirtualTemperature;
            float preControllableTemp = comp.GetPreControllableTemperature();
            float insulatedTemp = comp.GetInsulatedTemperature();
            float outdoorTemp = staircase.Map?.mapTemperature.OutdoorTemp ?? 21f;
            
            string tempLabel = $"{currentTemp.ToStringTemperature("F0")}";
            tempLabel += $" (Outdoor: {outdoorTemp.ToStringTemperature("F0")}";
            if (comp.HasAnyInsulatingModifier())
            {
                tempLabel += $", Insulated: {insulatedTemp.ToStringTemperature("F0")}";
            }
            if (comp.HasAnyDumbTempModifier())
            {
                // Show controllable fueled changers with throttle percentage
                if (comp.HasAnyControllableFueledTempChanger())
                {
                    float utilizationRatio = comp.GetFueledUtilizationRatio();
                    int throttlePercent = Mathf.RoundToInt(utilizationRatio * 100f);
                    tempLabel += $", Fueled: {preControllableTemp.ToStringTemperature("F0")} ({throttlePercent}%)";
                }
                else
                {
                    tempLabel += $", Passive: {preControllableTemp.ToStringTemperature("F0")}";
                }
            }
            if (comp.HasAnySmartTempModifier())
            {
                tempLabel += $", Active: {currentTemp.ToStringTemperature("F0")}";
            }
            tempLabel += ")";
            listing.Label("SF_Stat_Temperature".Translate(tempLabel));
            
            // Target temperature slider (show if has smart temp modifiers OR controllable fueled changers)
            if (showSlider && (comp.HasAnySmartTempModifier() || comp.HasAnyControllableFueledTempChanger()))
            {
                Rect sliderRect = listing.GetRect(TabLayout.SliderHeight);
                Rect labelRect = new Rect(sliderRect.x, sliderRect.y, 120f, sliderRect.height);
                Rect actualSliderRect = new Rect(labelRect.xMax, sliderRect.y, sliderRect.width - 120f, sliderRect.height);
                
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, "SF_TargetTemp".Translate() + ": " + comp.targetTemperature.ToStringTemperature("F0"));
                Text.Anchor = TextAnchor.UpperLeft;
                
                float newTarget = Widgets.HorizontalSlider(actualSliderRect, comp.targetTemperature, -10f, 40f, true, null, "SF_TempMin".Translate(), "SF_TempMax".Translate(), 1f);
                if (newTarget != comp.targetTemperature)
                {
                    comp.targetTemperature = newTarget;
                }
            }
        }
        
        /// <summary>
        /// Gets the room type label based on upgrades.
        /// </summary>
        private static string GetRoomTypeLabel(Thing staircase, CompStaircaseUpgrades comp)
        {
            bool isBarracks = comp.IsBarracks;
            
            // Check if this is a basement via mod extension
            var modExt = staircase.def.GetModExtension<SecondFloorModExtension>();
            bool isBasement = modExt != null && modExt.HasBasementExpansion;
            
            if (isBasement)
            {
                return isBarracks ? "SF_RoomType_BasementBarracks".Translate() : "SF_RoomType_Basement".Translate();
            }
            
            if (isBarracks)
            {
                return "SF_RoomType_Barracks".Translate();
            }
            
            if (comp.BedCount >= 4)
            {
                return "SF_RoomType_MultipleRooms".Translate();
            }
            
            return "SF_RoomType_SingleBedroom".Translate();
        }
        
        /// <summary>
        /// Gets the impressiveness label based on upgrades.
        /// </summary>
        private static string GetImpressivenessLabel(CompStaircaseUpgrades comp)
        {
            int totalImpressivenessBonus = 0;
            foreach (var upgrade in comp.GetActiveUpgradeDefs())
            {
                totalImpressivenessBonus += upgrade.impressivenessLevel;
            }
            int impressivenessLevel = Mathf.Clamp(1 + totalImpressivenessBonus, 0, 9);
            
            string[] impressivenessLabels = new string[]
            {
                "SF_Impressiveness_Awful".Translate(),
                "SF_Impressiveness_Dull".Translate(),
                "SF_Impressiveness_Mediocre".Translate(),
                "SF_Impressiveness_Decent".Translate(),
                "SF_Impressiveness_SlightlyImpressive".Translate(),
                "SF_Impressiveness_Impressive".Translate(),
                "SF_Impressiveness_VeryImpressive".Translate(),
                "SF_Impressiveness_ExtremelyImpressive".Translate(),
                "SF_Impressiveness_UnbelievablyImpressive".Translate(),
                "SF_Impressiveness_WondrouslyImpressive".Translate()
            };
            
            return impressivenessLabels[impressivenessLevel];
        }
        
        /// <summary>
        /// Gets total comfort from all active upgrades plus base bed comfort.
        /// The Harmony patch already applies upgrade bonuses to GetStatValue, so we just return that.
        /// </summary>
        private static float GetTotalComfort(Thing staircase, CompStaircaseUpgrades comp)
        {
            // GetStatValue already includes upgrade bonuses via the Harmony patch
            return staircase.GetStatValue(StatDefOf.Comfort);
        }
        
        /// <summary>
        /// Gets total rest effectiveness from all active upgrades plus base.
        /// The Harmony patch already applies upgrade bonuses to GetStatValue, so we just return that.
        /// </summary>
        private static float GetTotalRestEffectiveness(Thing staircase, CompStaircaseUpgrades comp)
        {
            // GetStatValue already includes upgrade bonuses via the Harmony patch
            return staircase.GetStatValue(StatDefOf.BedRestEffectiveness);
        }
    }
}
