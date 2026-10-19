using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace BenchNetwork
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Build placement and repair checks run on each client, so every player needs the same version.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "m0zzy.BenchNetwork";
        public const string PluginName = "BenchNetwork";
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private bool _configChanged;

        private void Awake()
        {
            Log = Logger;

            ModConfig.Bind(Config);
            Config.SettingChanged += (_, __) => _configChanged = true;
            SynchronizationManager.OnConfigurationSynchronized += (_, __) => _configChanged = true;

            _harmony = new Harmony(PluginGuid);
            ApplyPatches();
        }

        private void Update()
        {
            // A server sync changes many entries at once; handle them together.
            if (_configChanged)
            {
                _configChanged = false;
                ModConfig.Reload();
                StationRanges.ApplyToAll();
                StationNetworks.MarkDirty();
            }

            StationNetworks.Tick();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Patch class by class so a method renamed in a game update only disables that one feature.
        private void ApplyPatches()
        {
            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                {
                    continue;
                }

                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogError($"Could not apply {type.Name}; that feature is disabled. {e.GetBaseException().Message}");
                }
            }
        }
    }
}
