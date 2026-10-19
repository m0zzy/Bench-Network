using HarmonyLib;

namespace BenchNetwork.Patches
{
    // Keep shield radius and networks current as generators load, get built, or get destroyed.

    [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.Start))]
    internal static class ShieldGeneratorStartPatch
    {
        private static void Postfix(ShieldGenerator __instance)
        {
            try
            {
                ShieldNetworks.Apply(__instance);
                ShieldNetworks.MarkDirty();
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.OnDestroy))]
    internal static class ShieldGeneratorOnDestroyPatch
    {
        private static void Postfix(ShieldGenerator __instance)
        {
            try
            {
                ShieldNetworks.Forget(__instance);
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }
    }

    /// <summary>Adds the generator's shield network and its shared fuel to the hover text.</summary>
    [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.GetHoverText))]
    internal static class ShieldGeneratorHoverTextPatch
    {
        private static void Postfix(ShieldGenerator __instance, ref string __result)
        {
            try
            {
                if (!ModConfig.Enabled.Value || !ModConfig.ShowNetworkInHoverText.Value)
                {
                    return;
                }

                string line = ShieldNetworks.Describe(__instance);
                if (line != null)
                {
                    __result += "\n" + line;
                }
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }
    }
}
