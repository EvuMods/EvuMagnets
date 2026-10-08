using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace EvuMagnets;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
[BepInDependency("Azumatt.AzuExtendedPlayerInventory", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static PluginConfig Settings { get; private set; } = null!;

    internal static BepInEx.Logging.ManualLogSource Log { get; private set; } = null!;

    ConfigFileWatcher _configWatcher = null!;

    void Awake()
    {
        Log = Logger;
        Settings = new PluginConfig(Config);
        _configWatcher = new ConfigFileWatcher(Config);
        MagnetItems.Bind(Settings);
#pragma warning disable CS0618
        ItemManager.OnVanillaItemsAvailable += MagnetItems.Register;
#pragma warning restore CS0618
        ItemManager.OnItemsRegistered += MagnetItems.ApplyRecipes;
        SynchronizationManager.OnConfigurationSynchronized += (_, __) => MagnetItems.ApplyRecipes();

        Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginInfo.Guid);
        Logger.LogInfo("EvuMagnets loaded.");
    }

    void Update()
    {
        MagnetEquip.ReconcileAfterLoad(Player.m_localPlayer);
        if (Settings == null || !Settings.Toggle.Value.IsDown() || Typing())
        {
            return;
        }

        Settings.Active.Value = !Settings.Active.Value;
    }

    static bool Typing()
    {
        if (Console.instance != null && Console.IsVisible())
        {
            return true;
        }

        if (Chat.instance != null && Chat.instance.HasFocus())
        {
            return true;
        }

        return TextInput.instance != null && TextInput.IsVisible();
    }
}
