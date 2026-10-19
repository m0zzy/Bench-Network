using HarmonyLib;
using UnityEngine;

namespace BenchNetwork.Patches
{
    /// <summary>
    /// Placing, removing and hammer-repairing pieces all ask the game for a station of the piece's type
    /// near the player. When the game finds none, answer with the network's station of that type instead.
    /// </summary>
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.HaveBuildStationInRange))]
    internal static class HaveBuildStationInRangePatch
    {
        // Positional arguments (name, point) so the patch doesn't depend on the game's parameter names.
        private static void Postfix(string __0, Vector3 __1, ref CraftingStation __result)
        {
            if (__result || !ModConfig.Enabled.Value || !ModConfig.LinkBuilding.Value)
            {
                return;
            }

            __result = StationNetworks.FindLinkedStation(__0, __1);
        }
    }
}
