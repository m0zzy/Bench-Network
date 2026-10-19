using HarmonyLib;

namespace BenchNetwork.Patches
{
    // Keep ranges and networks current as stations and upgrades load, get placed, or get destroyed.

    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Start))]
    internal static class CraftingStationStartPatch
    {
        // Also runs for placement ghosts, so the circle shown while placing matches the configured range.
        private static void Postfix(CraftingStation __instance)
        {
            try
            {
                StationRanges.Apply(__instance);
                StationNetworks.MarkDirty();
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }
    }

    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.OnDestroy))]
    internal static class CraftingStationOnDestroyPatch
    {
        private static void Postfix(CraftingStation __instance)
        {
            try
            {
                StationRanges.Forget(__instance);
                StationNetworks.MarkDirty();
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }
    }

    [HarmonyPatch(typeof(StationExtension), nameof(StationExtension.Awake))]
    internal static class StationExtensionAwakePatch
    {
        private static void Postfix() => StationNetworks.MarkLevelsChanged(); // only sets flags; cannot throw
    }

    [HarmonyPatch(typeof(StationExtension), nameof(StationExtension.OnDestroy))]
    internal static class StationExtensionOnDestroyPatch
    {
        private static void Postfix() => StationNetworks.MarkLevelsChanged(); // only sets flags; cannot throw
    }
}
