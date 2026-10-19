using System.Collections.Generic;

namespace BenchNetwork
{
    /// <summary>
    /// While the hammer is equipped, keeps the range circle of every station in the player's network visible,
    /// and handles the key that turns this on or off.
    /// </summary>
    internal static class NetworkHighlight
    {
        private static readonly List<CraftingStation> Shown = new List<CraftingStation>();
        private static StationNetwork _shownNetwork;

        /// <summary>Called every frame by the plugin, after the network cache is updated.</summary>
        public static void Update()
        {
            HandleToggleKey();

            Player player = Player.m_localPlayer;
            StationNetwork target = null;
            if (ModConfig.Enabled.Value && ModConfig.ShowNetworkWhileBuilding.Value && player && player.InPlaceMode())
            {
                target = StationNetworks.CachedNetworkAt(player.transform.position);
            }

            if (target != _shownNetwork)
            {
                HideAll();
                _shownNetwork = target;
                if (target != null)
                {
                    Shown.AddRange(target.Members);
                }
            }

            // Re-activate every frame: the game hides a station's circle on its own timer after showing it.
            foreach (CraftingStation station in Shown)
            {
                if (station && station.m_areaMarker && !station.m_areaMarker.activeSelf)
                {
                    station.m_areaMarker.SetActive(true);
                }
            }
        }

        private static void HideAll()
        {
            foreach (CraftingStation station in Shown)
            {
                if (station && station.m_areaMarker)
                {
                    station.m_areaMarker.SetActive(false);
                }
            }

            Shown.Clear();
        }

        private static void HandleToggleKey()
        {
            Player player = Player.m_localPlayer;
            if (!player || IsTyping() || !ModConfig.ToggleNetworkCirclesKey.Value.IsDown())
            {
                return;
            }

            bool show = !ModConfig.ShowNetworkWhileBuilding.Value;
            ModConfig.ShowNetworkWhileBuilding.Value = show;
            player.Message(MessageHud.MessageType.TopLeft, show ? "Network circles: on" : "Network circles: off");
        }

        private static bool IsTyping() =>
            global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || (Chat.instance && Chat.instance.HasFocus());
    }
}
