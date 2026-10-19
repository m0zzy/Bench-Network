using HarmonyLib;
using UnityEngine;

namespace BenchNetwork.Patches
{
    /// <summary>
    /// Lets the station the player is using repair an item when the item's station type is in the same
    /// network at the recipe's minimum level. HaveRepairableItems and RepairOneItem both go through CanRepair.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.CanRepair))]
    internal static class CanRepairPatch
    {
        // __0 is the item; positional so the patch doesn't depend on the game's parameter name.
        private static void Postfix(ItemDrop.ItemData __0, ref bool __result)
        {
            // Never let a mod error break the game's own check; keep the game's answer instead.
            try
            {
                ItemDrop.ItemData item = __0;
                if (__result || !ModConfig.Enabled.Value || !ModConfig.LinkRepair.Value)
                {
                    return;
                }

                if (item?.m_shared == null || !item.m_shared.m_canBeReparied)
                {
                    return;
                }

                Player player = Player.m_localPlayer;
                CraftingStation current = player ? player.GetCurrentCraftingStation() : null;
                StationNetwork network = current ? StationNetworks.NetworkOf(current) : null;
                Recipe recipe = network != null && ObjectDB.instance ? ObjectDB.instance.GetRecipe(item) : null;
                if (!recipe)
                {
                    return;
                }

                int requiredLevel = Mathf.Max(1, recipe.m_minStationLevel);
                __result = HasStation(network, recipe.m_repairStation, requiredLevel)
                    || HasStation(network, recipe.m_craftingStation, requiredLevel);

                if (Diagnostics.Verbose)
                {
                    Diagnostics.DebugOnChange("repair:" + item.m_shared.m_name,
                        $"Repair check for {item.m_shared.m_name} at the {current.m_name}: {(__result ? "allowed" : "refused")} " +
                        $"(needs level {requiredLevel} of {Describe(network, recipe.m_repairStation)} or {Describe(network, recipe.m_craftingStation)}).");
                }
            }
            catch (System.Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }

        private static string Describe(StationNetwork network, CraftingStation required) =>
            required ? $"{required.m_name} (network has level {network.LevelOf(required.m_name)})" : "none";

        private static bool HasStation(StationNetwork network, CraftingStation required, int level) =>
            required && network.LevelOf(required.m_name) >= level;
    }
}
