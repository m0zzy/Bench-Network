using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace BenchNetwork
{
    /// <summary>
    /// Private game members, read through reflection. Unlike direct access through the publicized assembly,
    /// reflection isn't subject to the runtime's access checks, so this can't throw FieldAccessException.
    /// </summary>
    internal static class GameAccess
    {
        private static readonly FieldInfo AllStationsField = AccessTools.Field(typeof(CraftingStation), "m_allStations");
        private static readonly List<CraftingStation> NoStations = new List<CraftingStation>();

        /// <summary>The game's list of loaded crafting stations (CraftingStation.m_allStations).</summary>
        public static List<CraftingStation> AllStations =>
            AllStationsField?.GetValue(null) as List<CraftingStation> ?? NoStations;

        /// <summary>Logs at startup whether every member could be found, so a game update shows up in the log.</summary>
        public static void CheckMembers()
        {
            if (AllStationsField == null)
            {
                Plugin.Log.LogError("CraftingStation.m_allStations not found; networks are disabled. The game has probably changed.");
            }
        }
    }
}
