using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BenchNetwork
{
    /// <summary>A group of stations whose range circles overlap, directly or through other members.</summary>
    internal sealed class StationNetwork
    {
        private struct Circle
        {
            public float X;
            public float Z;
            public float RangeSqr;
            public float Range;
            public int Level;
        }

        public readonly List<CraftingStation> Members = new List<CraftingStation>();

        private readonly List<Circle> _circles = new List<Circle>();
        private readonly Dictionary<string, CraftingStation> _bestStation = new Dictionary<string, CraftingStation>();
        private readonly Dictionary<string, int> _bestLevel = new Dictionary<string, int>();
        private string _description;

        internal void Add(CraftingStation station, Vector3 position, float range, int level)
        {
            Members.Add(station);
            _circles.Add(new Circle { X = position.x, Z = position.z, RangeSqr = range * range, Range = range, Level = level });

            if (!_bestLevel.TryGetValue(station.m_name, out int best) || level > best)
            {
                _bestLevel[station.m_name] = level;
                _bestStation[station.m_name] = station;
            }
        }

        public bool Contains(string type) => _bestLevel.ContainsKey(type);

        /// <summary>Highest level of this station type in the network, or 0 when the type is missing.</summary>
        public int LevelOf(string type) => _bestLevel.TryGetValue(type, out int level) ? level : 0;

        /// <summary>The best-upgraded station of this type in the network, or null.</summary>
        public CraftingStation BestStation(string type) => _bestStation.TryGetValue(type, out CraftingStation station) ? station : null;

        /// <summary>True when the point lies inside any member's build circle. Height is ignored, as in the game.</summary>
        public bool Covers(Vector3 point)
        {
            foreach (Circle circle in _circles)
            {
                float dx = point.x - circle.X;
                float dz = point.z - circle.Z;
                if (dx * dx + dz * dz < circle.RangeSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One line per member, for logs: name token, level, range and map position.</summary>
        public void AppendDetails(StringBuilder text)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                Circle circle = _circles[i];
                string name = Members[i] ? Members[i].m_name : "(destroyed)";
                text.Append("\n  ").Append(name)
                    .Append(" level ").Append(circle.Level)
                    .Append(", range ").Append(circle.Range.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(" at (").Append(circle.X.ToString("0", CultureInfo.InvariantCulture))
                    .Append(", ").Append(circle.Z.ToString("0", CultureInfo.InvariantCulture)).Append(')');
            }
        }

        /// <summary>e.g. "Network: Workbench 5, Forge 7, Stonecutter". Built once per rebuild.</summary>
        public string Description
        {
            get
            {
                if (_description != null)
                {
                    return _description;
                }

                var types = new List<string>(_bestLevel.Keys);
                types.Sort((a, b) =>
                {
                    int order = ModConfig.OrderOf(a).CompareTo(ModConfig.OrderOf(b));
                    return order != 0 ? order : string.CompareOrdinal(a, b);
                });

                var text = new StringBuilder("Network: ");
                for (int i = 0; i < types.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(Localization.instance.Localize(types[i]));
                    int level = _bestLevel[types[i]];
                    if (level > 1)
                    {
                        text.Append(' ').Append(level);
                    }
                }

                _description = text.ToString();
                return _description;
            }
        }
    }

    /// <summary>Builds and caches station networks from the loaded crafting stations.</summary>
    internal static class StationNetworks
    {
        // The game re-counts a station's upgrades on its own timer (about 2 s), so rebuild again after this delay.
        private const float LevelSettleDelay = 2.5f;

        private static readonly List<StationNetwork> Networks = new List<StationNetwork>();
        private static readonly Dictionary<CraftingStation, StationNetwork> ByStation = new Dictionary<CraftingStation, StationNetwork>();

        // Scratch buffers reused by every rebuild.
        private static readonly List<CraftingStation> Stations = new List<CraftingStation>();
        private static readonly List<Vector3> Positions = new List<Vector3>();
        private static readonly List<float> RangeList = new List<float>();
        private static readonly List<int> Levels = new List<int>();
        private static readonly List<int> Parent = new List<int>();
        private static readonly Dictionary<int, StationNetwork> ByRoot = new Dictionary<int, StationNetwork>();

        private static bool _dirty = true;
        private static float _settleAt = -1f;
        private static float _nextPeriodicRebuild;

        public static void MarkDirty() => _dirty = true;

        /// <summary>For upgrade changes: rebuild now, and again once the game has re-counted station levels.</summary>
        public static void MarkLevelsChanged()
        {
            _dirty = true;
            _settleAt = Time.time + LevelSettleDelay;
        }

        /// <summary>Called every frame by the plugin.</summary>
        public static void Tick()
        {
            if (!ModConfig.Enabled.Value)
            {
                if (Networks.Count > 0)
                {
                    Clear();
                }

                return;
            }

            // Only players build and repair; a dedicated server or the main menu needs no networks.
            Player player = Player.m_localPlayer;
            if (!player)
            {
                return;
            }

            float now = Time.time;
            if (_settleAt >= 0f && now >= _settleAt)
            {
                _settleAt = -1f;
                _dirty = true;
            }

            if (player.InPlaceMode() && now >= _nextPeriodicRebuild)
            {
                _dirty = true;
            }

            if (_dirty)
            {
                Rebuild();
            }
        }

        /// <summary>The network the station belongs to, rebuilding first if something changed.</summary>
        public static StationNetwork NetworkOf(CraftingStation station)
        {
            EnsureFresh();
            return CachedNetworkOf(station);
        }

        /// <summary>The network the station belonged to at the last rebuild. Never rebuilds.</summary>
        public static StationNetwork CachedNetworkOf(CraftingStation station) =>
            station && ByStation.TryGetValue(station, out StationNetwork network) ? network : null;

        /// <summary>
        /// The best station of the given type in a network whose circles cover the point, or null.
        /// </summary>
        public static CraftingStation FindLinkedStation(string type, Vector3 point)
        {
            if (!ModConfig.IsLinkedType(type))
            {
                return null;
            }

            EnsureFresh();
            foreach (StationNetwork network in Networks)
            {
                if (network.Contains(type) && network.Covers(point))
                {
                    return network.BestStation(type);
                }
            }

            return null;
        }

        /// <summary>
        /// The network whose circles cover the point at the last rebuild, or null. Never rebuilds.
        /// Overlapping circles always share a network, so at most one network covers a point.
        /// </summary>
        public static StationNetwork CachedNetworkAt(Vector3 point)
        {
            foreach (StationNetwork network in Networks)
            {
                if (network.Covers(point))
                {
                    return network;
                }
            }

            return null;
        }

        /// <summary>Every network with its members, for the log and the benchnetwork command. Rebuilds first if needed.</summary>
        public static string Describe()
        {
            EnsureFresh();
            return DescribeCache();
        }

        /// <summary>1-based position of the network in <see cref="Describe"/>'s list.</summary>
        public static int IndexOf(StationNetwork network) => Networks.IndexOf(network) + 1;

        private static string DescribeCache()
        {
            if (Networks.Count == 0)
            {
                return "No linked stations are loaded.";
            }

            var text = new StringBuilder();
            text.Append(Networks.Count).Append(Networks.Count == 1 ? " network:" : " networks:");
            for (int i = 0; i < Networks.Count; i++)
            {
                text.Append("\nNetwork ").Append(i + 1).Append(" (").Append(Networks[i].Members.Count).Append(" stations):");
                Networks[i].AppendDetails(text);
            }

            return text.ToString();
        }

        private static void EnsureFresh()
        {
            if (_dirty && ModConfig.Enabled.Value)
            {
                Rebuild();
            }
        }

        private static void Clear()
        {
            Networks.Clear();
            ByStation.Clear();
            _dirty = true;
        }

        private static void Rebuild()
        {
            _dirty = false;
            _nextPeriodicRebuild = Time.time + ModConfig.RefreshInterval.Value;

            Networks.Clear();
            ByStation.Clear();
            Stations.Clear();
            Positions.Clear();
            RangeList.Clear();
            Levels.Clear();
            Parent.Clear();
            ByRoot.Clear();

            foreach (CraftingStation station in GameAccess.AllStations)
            {
                if (!station || !ModConfig.IsLinkedType(station.m_name))
                {
                    continue;
                }

                int level = station.GetLevel();
                float range = StationRanges.EffectiveRange(station, level);
                StationRanges.UpdateMarker(station, range);

                Parent.Add(Stations.Count);
                Stations.Add(station);
                Positions.Add(station.transform.position);
                RangeList.Add(range);
                Levels.Add(level);
            }

            // Union-find: link every pair whose circles overlap (distance < rangeA + rangeB).
            int count = Stations.Count;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float dx = Positions[i].x - Positions[j].x;
                    float dz = Positions[i].z - Positions[j].z;
                    float reach = RangeList[i] + RangeList[j];
                    if (dx * dx + dz * dz < reach * reach)
                    {
                        Union(i, j);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                int root = Find(i);
                if (!ByRoot.TryGetValue(root, out StationNetwork network))
                {
                    network = new StationNetwork();
                    ByRoot[root] = network;
                    Networks.Add(network);
                }

                network.Add(Stations[i], Positions[i], RangeList[i], Levels[i]);
                ByStation[Stations[i]] = network;
            }

            if (Diagnostics.Verbose)
            {
                Diagnostics.DebugOnChange("networks", "Networks changed. " + DescribeCache());
            }

            // Drop references to game objects until the next rebuild.
            Stations.Clear();
        }

        private static int Find(int i)
        {
            while (Parent[i] != i)
            {
                Parent[i] = Parent[Parent[i]];
                i = Parent[i];
            }

            return i;
        }

        private static void Union(int a, int b)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if (rootA != rootB)
            {
                Parent[rootB] = rootA;
            }
        }
    }
}
