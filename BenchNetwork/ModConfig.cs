using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace BenchNetwork
{
    /// <summary>Build range for one station type. A negative value keeps the game's own value.</summary>
    internal struct RangeSetting
    {
        public float BaseRange;
        public float RangePerLevel;
    }

    internal static class ModConfig
    {
        private const string GeneralSection = "1 - General";
        private const string RangeSection = "2 - Ranges";
        private const string ClientSection = "3 - Client";

        private sealed class StationRangeEntries
        {
            public string Token;
            public string Key;
            public ConfigEntry<float> BaseRange;
            public ConfigEntry<float> RangePerLevel;
        }

        private static readonly StationRangeEntries[] VanillaStations =
        {
            new StationRangeEntries { Token = "$piece_workbench", Key = "Workbench" },
            new StationRangeEntries { Token = "$piece_forge", Key = "Forge" },
            new StationRangeEntries { Token = "$piece_stonecutter", Key = "Stonecutter" },
            new StationRangeEntries { Token = "$piece_artisanstation", Key = "Artisan table" },
            new StationRangeEntries { Token = "$piece_blackforge", Key = "Black forge" },
            new StationRangeEntries { Token = "$piece_magetable", Key = "Galdr table" },
        };

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> LinkBuilding;
        public static ConfigEntry<bool> LinkRepair;
        public static ConfigEntry<string> ExtraStations;
        public static ConfigEntry<float> RefreshInterval;
        public static ConfigEntry<bool> ShowNetworkInHoverText;

        // Parsed view of the entries above, rebuilt by Reload().
        private static readonly Dictionary<string, RangeSetting> Ranges = new Dictionary<string, RangeSetting>();
        private static readonly Dictionary<string, int> TypeOrder = new Dictionary<string, int>();

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(GeneralSection, "Enabled", true, Synced(
                "Master switch. When off, station ranges go back to the game's values and nothing is linked."));
            LinkBuilding = config.Bind(GeneralSection, "Link building", true, Synced(
                "Inside a network, build, remove and repair pieces as if every station type in the network were in range."));
            LinkRepair = config.Bind(GeneralSection, "Link repair", true, Synced(
                "At any station, repair items whose station type is in the same network at a high enough level."));
            ExtraStations = config.Bind(GeneralSection, "Extra stations", "", Synced(
                "Stations added by other mods that should join networks, comma separated. " +
                "Each entry is a name token with an optional range and range per level, " +
                "e.g. \"$piece_mystation\" or \"$piece_mystation:30:4\". Missing or negative numbers keep the station's own value."));

            foreach (StationRangeEntries station in VanillaStations)
            {
                string label = station.Key.ToLowerInvariant();
                station.BaseRange = config.Bind(RangeSection, $"{station.Key} range", -1f, Synced(
                    $"Build range of the {label} ({station.Token}) at level 1. -1 keeps the game's value.",
                    new AcceptableValueRange<float>(-1f, 200f)));
                station.RangePerLevel = config.Bind(RangeSection, $"{station.Key} range per level", -1f, Synced(
                    $"Build range the {label} gains per upgrade level. -1 keeps the game's value.",
                    new AcceptableValueRange<float>(-1f, 50f)));
            }

            RefreshInterval = config.Bind(ClientSection, "Network refresh interval", 2f, new ConfigDescription(
                "Seconds between network rebuilds while the hammer is equipped. Placing or destroying a station always rebuilds right away.",
                new AcceptableValueRange<float>(0.5f, 30f)));
            ShowNetworkInHoverText = config.Bind(ClientSection, "Show network in hover text", true,
                "Add a line listing the station types in a station's network, with their highest level, to its hover text.");

            Reload();
        }

        /// <summary>Re-reads the range entries and the extra stations list.</summary>
        public static void Reload()
        {
            Ranges.Clear();
            TypeOrder.Clear();

            foreach (StationRangeEntries station in VanillaStations)
            {
                Add(station.Token, station.BaseRange.Value, station.RangePerLevel.Value);
            }

            foreach (string entry in ExtraStations.Value.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                string token = parts[0].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                Add(token, ParseRange(parts, 1, entry), ParseRange(parts, 2, entry));
            }
        }

        /// <summary>True when stations with this name token take part in networks.</summary>
        public static bool IsLinkedType(string token) => token != null && Ranges.ContainsKey(token);

        public static bool TryGetRange(string token, out RangeSetting setting)
        {
            if (token == null)
            {
                setting = default;
                return false;
            }

            return Ranges.TryGetValue(token, out setting);
        }

        /// <summary>Display order of a station type: vanilla stations first, then the extra stations list.</summary>
        public static int OrderOf(string token) => TypeOrder.TryGetValue(token, out int order) ? order : int.MaxValue;

        private static void Add(string token, float baseRange, float rangePerLevel)
        {
            if (!TypeOrder.ContainsKey(token))
            {
                TypeOrder[token] = TypeOrder.Count;
            }

            Ranges[token] = new RangeSetting { BaseRange = baseRange, RangePerLevel = rangePerLevel };
        }

        private static float ParseRange(string[] parts, int index, string entry)
        {
            if (parts.Length <= index || parts[index].Trim().Length == 0)
            {
                return -1f;
            }

            if (float.TryParse(parts[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return value;
            }

            Plugin.Log.LogWarning($"Extra stations: could not read \"{parts[index]}\" in \"{entry.Trim()}\"; keeping the station's own value.");
            return -1f;
        }

        // Admin-only entries are pushed from the server by Jotunn and locked for everyone else.
        private static ConfigDescription Synced(string description, AcceptableValueBase acceptableValues = null) =>
            new ConfigDescription(description, acceptableValues, new ConfigurationManagerAttributes { IsAdminOnly = true });
    }
}
