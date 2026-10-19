using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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
        private static readonly FieldInfo SegmentsField = AccessTools.Field(typeof(CircleProjector), "m_segments");

        /// <summary>The game's list of loaded crafting stations (CraftingStation.m_allStations).</summary>
        public static List<CraftingStation> AllStations =>
            AllStationsField?.GetValue(null) as List<CraftingStation> ?? NoStations;

        /// <summary>The ring pieces a range circle is drawn with (CircleProjector.m_segments), or null.</summary>
        public static List<GameObject> Segments(CircleProjector projector) =>
            projector ? SegmentsField?.GetValue(projector) as List<GameObject> : null;

        /// <summary>Logs at startup whether every member could be found, so a game update shows up in the log.</summary>
        public static void CheckMembers()
        {
            if (AllStationsField == null)
            {
                Plugin.Log.LogError("CraftingStation.m_allStations not found; networks are disabled. The game has probably changed.");
            }

            if (SegmentsField == null)
            {
                Plugin.Log.LogWarning("CircleProjector.m_segments not found; network outlines show full circles instead.");
            }
        }
    }
}
