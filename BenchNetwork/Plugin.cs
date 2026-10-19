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
            SynchronizationManager.OnConfigurationSynchronized += (_, args) =>
            {
                Log.LogInfo(args.InitialSynchronization ? "Received settings from the server." : "Server settings updated.");
                _configChanged = true;
            };

            _harmony = new Harmony(PluginGuid);
            ApplyPatches();
            GameAccess.CheckMembers();
            CommandManager.Instance.AddConsoleCommand(new BenchNetworkCommand());
            Diagnostics.InfoOnChange("settings", ModConfig.Describe());
        }

        private void Update()
        {
            // Runs every frame: log a failure once instead of on every frame.
            try
            {
                // A server sync changes many entries at once; handle them together.
                if (_configChanged)
                {
                    _configChanged = false;
                    ModConfig.Reload();
                    Diagnostics.InfoOnChange("settings", ModConfig.Describe());
                    StationRanges.ApplyToAll();
                    StationNetworks.MarkDirty();
                }

                StationNetworks.Tick();
                NetworkHighlight.Update();
            }
            catch (Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }

        // After every Update, so the game has already placed this frame's circle segments.
        private void LateUpdate()
        {
            try
            {
                NetworkHighlight.LateUpdate();
            }
            catch (Exception e)
            {
                Diagnostics.ErrorOnce(e);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Patch class by class so a method renamed in a game update only disables that one feature.
        private void ApplyPatches()
        {
            int applied = 0;
            int failed = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                {
                    continue;
                }

                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed++;
                    Log.LogError($"Could not apply {type.Name}; that feature is disabled. {e.GetBaseException().Message}");
                }
            }

            string summary = $"BenchNetwork {PluginVersion} loaded: {applied} of {applied + failed} patches applied.";
            if (failed > 0)
            {
                Log.LogWarning(summary + " See the errors above.");
            }
            else
            {
                Log.LogInfo(summary);
            }
        }
    }
}
