using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace DraftAnimals
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    public class DraftAnimalsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "draftanimals";
        public const string PluginName = "Draft Animals";
        public const string PluginVersion = "1.1.2";

        private Harmony m_harmony;
        private ConfigFileWatcher m_configWatcher;

        private void Awake()
        {
            DraftConfig.Bind(Config);
            // Правка .cfg во время игры применяется сразу (а на сервере — рассылается игрокам).
            m_configWatcher = new ConfigFileWatcher(Config);
            DraftLocalization.Register();

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            PrefabManager.OnPrefabsRegistered += ApplyRecipes;
            SynchronizationManager.OnConfigurationSynchronized += (_, _) => ApplyRecipes();
            DraftConfig.RecipesChanged += ApplyRecipes;

            m_harmony = new Harmony(PluginGuid);
            m_harmony.PatchAll(typeof(AiAccess));
            m_harmony.PatchAll(typeof(Patches));

            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} загружен");
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            Items.Register();
            Pieces.Register();
            Young.Register();
            LiveHare.Register();
        }

        private static void ApplyRecipes()
        {
            Items.ApplyRecipes();
            Pieces.ApplyRecipes();
        }

        private void OnDestroy()
        {
            m_harmony?.UnpatchSelf();
        }
    }
}
