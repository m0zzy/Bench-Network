using System.Collections.Generic;
using UnityEngine;

namespace BenchNetwork
{
    /// <summary>
    /// While the hammer is equipped, keeps the range circle of every station in the player's network visible,
    /// either in full or as the network's outer edge only, and handles the key that turns this on or off.
    /// </summary>
    internal static class NetworkHighlight
    {
        // A ring piece this far inside another member's circle counts as interior and is hidden in outline mode.
        private const float OutlineMargin = 0.25f;

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

        /// <summary>
        /// Called after every Update, once the game has placed this frame's ring pieces. In outline mode, hides
        /// the pieces that fall inside another member's circle so only the network's outer edge remains.
        /// </summary>
        public static void LateUpdate()
        {
            if (_shownNetwork == null)
            {
                return;
            }

            bool outline = ModConfig.NetworkCircles.Value == NetworkCircleStyle.Outline;
            for (int i = 0; i < Shown.Count; i++)
            {
                List<GameObject> segments = Shown[i] ? GameAccess.Segments(StationRanges.MarkerOf(Shown[i])) : null;
                if (segments == null)
                {
                    continue;
                }

                foreach (GameObject segment in segments)
                {
                    if (!segment)
                    {
                        continue;
                    }

                    bool visible = !outline || !_shownNetwork.InsideOtherMember(i, segment.transform.position, OutlineMargin);
                    if (segment.activeSelf != visible)
                    {
                        segment.SetActive(visible);
                    }
                }
            }
        }

        private static void HideAll()
        {
            foreach (CraftingStation station in Shown)
            {
                if (station && station.m_areaMarker)
                {
                    // Restore any ring pieces the outline hid, so the game's own circle display is complete.
                    List<GameObject> segments = GameAccess.Segments(StationRanges.MarkerOf(station));
                    if (segments != null)
                    {
                        foreach (GameObject segment in segments)
                        {
                            if (segment && !segment.activeSelf)
                            {
                                segment.SetActive(true);
                            }
                        }
                    }

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
