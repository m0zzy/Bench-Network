using HarmonyLib;

namespace BenchNetwork.Patches
{
    /// <summary>Appends the station's network to its hover text, read from the cache only.</summary>
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetHoverText))]
    internal static class CraftingStationHoverTextPatch
    {
        private static void Postfix(CraftingStation __instance, ref string __result)
        {
            if (!ModConfig.Enabled.Value || !ModConfig.ShowNetworkInHoverText.Value)
            {
                return;
            }

            StationNetwork network = StationNetworks.CachedNetworkOf(__instance);
            if (network == null || network.Members.Count < 2)
            {
                return;
            }

            __result += "\n" + network.Description;
        }
    }
}
