using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BenchNetwork
{
    /// <summary>
    /// Links shield generators whose domes overlap into networks and keeps their fuel balanced, so the network
    /// behaves as one shared fuel pool. The game already draws overlapping domes as one blended bubble.
    /// Also applies the configured dome radius.
    /// </summary>
    internal static class ShieldNetworks
    {
        private const float TickInterval = 1f;

        // Fuel differences smaller than this aren't worth a network update.
        private const float FuelTolerance = 0.01f;

        private struct Original
        {
            public float MinRadius;
            public float MaxRadius;
        }

        private static readonly Dictionary<ShieldGenerator, Original> Originals = new Dictionary<ShieldGenerator, Original>();
        private static readonly List<List<ShieldGenerator>> Networks = new List<List<ShieldGenerator>>();
        private static readonly Dictionary<ShieldGenerator, List<ShieldGenerator>> ByGenerator =
            new Dictionary<ShieldGenerator, List<ShieldGenerator>>();

        // Scratch buffers reused by every rebuild.
        private static readonly List<ShieldGenerator> Generators = new List<ShieldGenerator>();
        private static readonly List<int> Parent = new List<int>();
        private static readonly Dictionary<int, List<ShieldGenerator>> ByRoot = new Dictionary<int, List<ShieldGenerator>>();

        private static float _nextTick;

        /// <summary>Applies the configured dome radius, keeping the game's values so they can be restored.</summary>
        public static void Apply(ShieldGenerator shield)
        {
            if (!Originals.TryGetValue(shield, out Original original))
            {
                original = new Original { MinRadius = shield.m_minShieldRadius, MaxRadius = shield.m_maxShieldRadius };
                Originals[shield] = original;
            }

            float max = original.MaxRadius;
            float min = original.MinRadius;
            if (ModConfig.Enabled.Value && ModConfig.ShieldRadius.Value >= 0f)
            {
                max = ModConfig.ShieldRadius.Value;
                min = max;
            }

            if (ModConfig.Enabled.Value && ModConfig.ShieldRadiusLowFuel.Value >= 0f)
            {
                min = Mathf.Min(ModConfig.ShieldRadiusLowFuel.Value, max);
            }

            shield.m_maxShieldRadius = max;
            shield.m_minShieldRadius = min;
        }

        public static void ApplyToAll()
        {
            foreach (ShieldGenerator shield in GameAccess.AllShields)
            {
                if (shield)
                {
                    Apply(shield);
                }
            }
        }

        public static void Forget(ShieldGenerator shield)
        {
            Originals.Remove(shield);
            _nextTick = 0f;
        }

        /// <summary>Rebuild right away on the next tick, e.g. after a generator is built or destroyed.</summary>
        public static void MarkDirty() => _nextTick = 0f;

        /// <summary>Called every frame by the plugin; does its work once a second.</summary>
        public static void Tick()
        {
            if (Time.time < _nextTick)
            {
                return;
            }

            _nextTick = Time.time + TickInterval;
            Rebuild();

            if (ModConfig.Enabled.Value && ModConfig.LinkShields.Value && GameAccess.CanShareFuel)
            {
                foreach (List<ShieldGenerator> network in Networks)
                {
                    BalanceFuel(network);
                }
            }
        }

        /// <summary>"Shield network: 3 generators, fuel 12.5 / 30", or null for a generator that isn't linked.</summary>
        public static string Describe(ShieldGenerator shield)
        {
            if (!ByGenerator.TryGetValue(shield, out List<ShieldGenerator> network) || network.Count < 2 || !GameAccess.CanShareFuel)
            {
                return null;
            }

            float fuel = 0f;
            int capacity = 0;
            foreach (ShieldGenerator member in network)
            {
                if (member)
                {
                    fuel += GameAccess.GetFuel(member);
                    capacity += member.m_maxFuel;
                }
            }

            return $"Shield network: {network.Count} generators, fuel {fuel.ToString("0.#", CultureInfo.InvariantCulture)} / {capacity}";
        }

        /// <summary>Every shield network, for the log and the benchnetwork command.</summary>
        public static string DescribeAll()
        {
            if (Networks.Count == 0)
            {
                return "No shield generators are loaded.";
            }

            var text = new StringBuilder();
            text.Append(Networks.Count).Append(Networks.Count == 1 ? " shield network:" : " shield networks:");
            for (int i = 0; i < Networks.Count; i++)
            {
                text.Append("\nShield network ").Append(i + 1).Append(" (").Append(Networks[i].Count).Append(" generators):");
                foreach (ShieldGenerator shield in Networks[i])
                {
                    if (!shield)
                    {
                        continue;
                    }

                    Vector3 at = shield.transform.position;
                    text.Append("\n  radius ").Append(shield.m_maxShieldRadius.ToString("0.#", CultureInfo.InvariantCulture))
                        .Append(", fuel ").Append(GameAccess.CanShareFuel ? GameAccess.GetFuel(shield).ToString("0.##", CultureInfo.InvariantCulture) : "?")
                        .Append('/').Append(shield.m_maxFuel)
                        .Append(", owned here=").Append(IsOwnedHere(shield) ? "yes" : "no")
                        .Append(" at (").Append(at.x.ToString("0", CultureInfo.InvariantCulture))
                        .Append(", ").Append(at.z.ToString("0", CultureInfo.InvariantCulture)).Append(')');
                }
            }

            return text.ToString();
        }

        private static void Rebuild()
        {
            Networks.Clear();
            ByGenerator.Clear();
            Generators.Clear();
            Parent.Clear();
            ByRoot.Clear();

            if (!ModConfig.Enabled.Value || !ModConfig.LinkShields.Value)
            {
                return;
            }

            foreach (ShieldGenerator shield in GameAccess.AllShields)
            {
                // Placement ghosts have no valid network object and never join.
                ZNetView view = shield ? shield.GetComponent<ZNetView>() : null;
                if (view && view.IsValid())
                {
                    Parent.Add(Generators.Count);
                    Generators.Add(shield);
                }
            }

            // Domes are spheres: link when they overlap in 3D (distance < radiusA + radiusB).
            int count = Generators.Count;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float reach = Generators[i].m_maxShieldRadius + Generators[j].m_maxShieldRadius;
                    if ((Generators[i].transform.position - Generators[j].transform.position).sqrMagnitude < reach * reach)
                    {
                        Union(i, j);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                int root = Find(i);
                if (!ByRoot.TryGetValue(root, out List<ShieldGenerator> network))
                {
                    network = new List<ShieldGenerator>();
                    ByRoot[root] = network;
                    Networks.Add(network);
                }

                network.Add(Generators[i]);
                ByGenerator[Generators[i]] = network;
            }

            Generators.Clear();

            if (Diagnostics.Verbose)
            {
                var sizes = new StringBuilder("Shield networks changed:");
                foreach (List<ShieldGenerator> network in Networks)
                {
                    sizes.Append(' ').Append(network.Count);
                }

                Diagnostics.DebugOnChange("shields", sizes.Append(" generator(s).").ToString());
            }
        }

        /// <summary>
        /// Spreads the network's total fuel evenly across its generators. Only the client that owns every
        /// generator in the network does this, so two players never fight over the same fuel.
        /// </summary>
        private static void BalanceFuel(List<ShieldGenerator> network)
        {
            if (network.Count < 2)
            {
                return;
            }

            float total = 0f;
            float capacity = 0f;
            foreach (ShieldGenerator shield in network)
            {
                if (!shield || !IsOwnedHere(shield))
                {
                    return;
                }

                total += GameAccess.GetFuel(shield);
                capacity += shield.m_maxFuel;
            }

            if (capacity <= 0f)
            {
                return;
            }

            // Each generator gets the same share of its own capacity, so the pool fills and drains together.
            float share = Mathf.Clamp01(total / capacity);
            bool changed = false;
            foreach (ShieldGenerator shield in network)
            {
                float target = share * shield.m_maxFuel;
                if (Mathf.Abs(GameAccess.GetFuel(shield) - target) > FuelTolerance)
                {
                    GameAccess.SetFuel(shield, target);
                    changed = true;
                }
            }

            if (changed && Diagnostics.Verbose)
            {
                Plugin.Log.LogInfo($"Shield network of {network.Count}: balanced {total.ToString("0.##", CultureInfo.InvariantCulture)} fuel across the network.");
            }
        }

        private static bool IsOwnedHere(ShieldGenerator shield)
        {
            ZNetView view = shield.GetComponent<ZNetView>();
            return view && view.IsValid() && view.IsOwner();
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
