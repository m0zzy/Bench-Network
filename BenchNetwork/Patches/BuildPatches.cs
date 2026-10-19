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

            if (Diagnostics.Verbose)
            {
                Vector3 at = __result ? __result.transform.position : Vector3.zero;
                Diagnostics.DebugOnChange("build:" + __0, __result
                    ? $"Build check for {__0}: allowed through the network by the {__result.m_name} at ({at.x:0}, {at.z:0})."
                    : $"Build check for {__0}: refused, no {__0} in range or in a network covering this spot.");
            }
        }
    }
}
