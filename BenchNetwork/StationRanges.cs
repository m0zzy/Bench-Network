using System.Collections.Generic;
using UnityEngine;

namespace BenchNetwork
{
    /// <summary>Applies the configured build ranges to stations and keeps their range circles in step.</summary>
    internal static class StationRanges
    {
        private struct Original
        {
            public float Range;
            public float RangePerLevel;
            public CircleProjector Marker;
            public float MarkerRadius;
            public int MarkerSegments;
        }

        // The game's values per station, captured before the first override so they can be restored.
        private static readonly Dictionary<CraftingStation, Original> Originals = new Dictionary<CraftingStation, Original>();

        public static void Apply(CraftingStation station)
        {
            if (!Originals.TryGetValue(station, out Original original))
            {
                original = Capture(station);
                Originals[station] = original;
            }

            float range = original.Range;
            float perLevel = original.RangePerLevel;

            if (ModConfig.Enabled.Value && ModConfig.TryGetRange(station.m_name, out RangeSetting setting))
            {
                if (setting.BaseRange >= 0f)
                {
                    range = setting.BaseRange;
                }

                if (setting.RangePerLevel >= 0f)
                {
                    perLevel = setting.RangePerLevel;
                }
            }

            station.m_rangeBuild = range;
            station.m_extraRangePerLevel = perLevel;

            if (ModConfig.Enabled.Value && ModConfig.IsLinkedType(station.m_name))
            {
                // Level 1 until the next network rebuild reads the real level.
                UpdateMarker(station, EffectiveRange(station, 1));
            }
            else if (original.Marker)
            {
                original.Marker.m_radius = original.MarkerRadius;
                original.Marker.m_nrOfSegments = original.MarkerSegments;
            }
        }

        public static void ApplyToAll()
        {
            foreach (CraftingStation station in GameAccess.AllStations)
            {
                if (station)
                {
                    Apply(station);
                }
            }
        }

        public static void Forget(CraftingStation station) => Originals.Remove(station);

        /// <summary>Build range at the given level, using the station's current (possibly overridden) values.</summary>
        public static float EffectiveRange(CraftingStation station, int level) =>
            station.m_rangeBuild + station.m_extraRangePerLevel * Mathf.Max(0, level - 1);

        /// <summary>Resizes the station's range circle, adding segments so a larger ring stays as dense.</summary>
        public static void UpdateMarker(CraftingStation station, float range)
        {
            if (!Originals.TryGetValue(station, out Original original) || !original.Marker)
            {
                return;
            }

            original.Marker.m_radius = range;
            if (original.MarkerRadius > 0f)
            {
                original.Marker.m_nrOfSegments = Mathf.Max(
                    original.MarkerSegments,
                    Mathf.RoundToInt(original.MarkerSegments * range / original.MarkerRadius));
            }
        }

        private static Original Capture(CraftingStation station)
        {
            var original = new Original
            {
                Range = station.m_rangeBuild,
                RangePerLevel = station.m_extraRangePerLevel,
            };

            if (station.m_areaMarker)
            {
                original.Marker = station.m_areaMarker.GetComponentInChildren<CircleProjector>(true);
                if (original.Marker)
                {
                    original.MarkerRadius = original.Marker.m_radius;
                    original.MarkerSegments = original.Marker.m_nrOfSegments;
                }
            }

            return original;
        }
    }
}
