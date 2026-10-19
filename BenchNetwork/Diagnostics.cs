using System.Collections.Generic;
using Jotunn.Entities;

namespace BenchNetwork
{
    /// <summary>Logging meant for troubleshooting from a player's BepInEx/LogOutput.log.</summary>
    internal static class Diagnostics
    {
        // Last message per key, so checks that run every frame only log when their outcome changes.
        private static readonly Dictionary<string, string> LastByKey = new Dictionary<string, string>();

        public static bool Verbose => ModConfig.DebugLogging != null && ModConfig.DebugLogging.Value;

        /// <summary>Logs at Info level (written to LogOutput.log by default) when the message for this key changes.</summary>
        public static void InfoOnChange(string key, string message)
        {
            if (LastByKey.TryGetValue(key, out string last) && last == message)
            {
                return;
            }

            LastByKey[key] = message;
            Plugin.Log.LogInfo(message);
        }

        /// <summary>Logs an exception the first time it occurs, so an error in per-frame code doesn't flood the log.</summary>
        public static void ErrorOnce(System.Exception exception)
        {
            string message = exception.ToString();
            if (LastByKey.TryGetValue("error:" + message, out _))
            {
                return;
            }

            LastByKey["error:" + message] = message;
            Plugin.Log.LogError("Unexpected error (logged once; it may keep happening): " + message);
        }

        /// <summary>Like <see cref="InfoOnChange"/>, but only with "Debug logging" turned on.</summary>
        public static void DebugOnChange(string key, string message)
        {
            if (Verbose)
            {
                InfoOnChange(key, message);
            }
        }
    }

    /// <summary>Console command "benchnetwork": writes the current settings and networks to the console and the log.</summary>
    internal sealed class BenchNetworkCommand : ConsoleCommand
    {
        public override string Name => "benchnetwork";

        public override string Help => "BenchNetwork: list the loaded station networks and current settings (also written to the log)";

        public override void Run(string[] args, Terminal context)
        {
            string report = $"BenchNetwork {Plugin.PluginVersion}\n{ModConfig.Describe()}\n{StationNetworks.Describe()}\n{ShieldNetworks.DescribeAll()}";
            Player player = Player.m_localPlayer;
            if (player)
            {
                StationNetwork here = StationNetworks.CachedNetworkAt(player.transform.position);
                report += here != null
                    ? $"\nYou are inside network {StationNetworks.IndexOf(here)}."
                    : "\nYou are not inside any network.";
            }

            Plugin.Log.LogInfo("benchnetwork command:\n" + report);
            foreach (string line in report.Split('\n'))
            {
                context?.AddString(line);
            }
        }
    }
}
