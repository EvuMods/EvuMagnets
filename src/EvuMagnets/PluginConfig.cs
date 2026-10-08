using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EvuMagnets.Core;
using UnityEngine;
using SyncHint = global::ConfigurationManagerAttributes;

namespace EvuMagnets;

internal sealed class TierConfig
{
    public TierConfig(ConfigFile config, MagnetTierInfo tier, int order)
    {
        var section = tier.EnglishName;
        Range1 = BindRange(config, section, "Range1", tier.Ranges[0], "Pickup radius in meters at quality 1.", order);
        if (!tier.Forged)
        {
            return;
        }

        Range2 = BindRange(config, section, "Range2", tier.Ranges[1], "Pickup radius in meters at quality 2.", order - 1);
        Range3 = BindRange(config, section, "Range3", tier.Ranges[2], "Pickup radius in meters at quality 3.", order - 2);
        Range4 = BindRange(config, section, "Range4", tier.Ranges[3], "Pickup radius in meters at quality 4.", order - 3);
        Craft = config.Bind(
            section,
            "Craft",
            tier.Craft,
            new ConfigDescription(
                "Ingredients for a new magnet. Item prefab names and amounts, separated by commas.",
                null,
                ConfigHints.Synced(order - 4)));
        Upgrades = config.Bind(
            section,
            "Upgrades",
            tier.Upgrades,
            new ConfigDescription(
                "Three upgrade steps, separated by semicolons. Each step is Item:Amount pairs separated by commas.",
                null,
                ConfigHints.Synced(order - 5)));
        Station = config.Bind(
            section,
            "Station",
            tier.Station,
            new ConfigDescription(
                "Crafting station prefab name.",
                null,
                ConfigHints.Synced(order - 6)));
        StationLevel = config.Bind(
            section,
            "StationLevel",
            tier.StationLevel,
            new ConfigDescription(
                "Crafting station level required to make quality 1. Each upgrade asks for one level more.",
                new AcceptableValueRange<int>(1, 7),
                ConfigHints.Synced(order - 7)));
    }

    public ConfigEntry<float> Range1 { get; }

    public ConfigEntry<float>? Range2 { get; }

    public ConfigEntry<float>? Range3 { get; }

    public ConfigEntry<float>? Range4 { get; }

    public ConfigEntry<string>? Craft { get; }

    public ConfigEntry<string>? Upgrades { get; }

    public ConfigEntry<string>? Station { get; }

    public ConfigEntry<int>? StationLevel { get; }

    public float[] Ranges => Range2 == null || Range3 == null || Range4 == null
        ? new[] { Range1.Value }
        : new[] { Range1.Value, Range2.Value, Range3.Value, Range4.Value };

    public void ListenRecipe(EventHandler handler)
    {
        if (Craft != null)
        {
            Craft.SettingChanged += handler;
        }

        if (Upgrades != null)
        {
            Upgrades.SettingChanged += handler;
        }

        if (Station != null)
        {
            Station.SettingChanged += handler;
        }

        if (StationLevel != null)
        {
            StationLevel.SettingChanged += handler;
        }
    }

    static ConfigEntry<float> BindRange(ConfigFile config, string section, string key, float value, string description, int order)
    {
        return config.Bind(
            section,
            key,
            value,
            new ConfigDescription(
                description,
                new AcceptableValueRange<float>(PickupRules.VanillaRange, 64f),
                ConfigHints.Synced(order)));
    }
}

internal sealed class PluginConfig
{
    public PluginConfig(ConfigFile config)
    {
        Enabled = config.Bind(
            "General",
            "Enabled",
            true,
            new ConfigDescription(
                "When off, magnets do not extend pickup range. Crafted magnets stay in the world.",
                null,
                ConfigHints.Synced(100)));
        PullThroughAllWards = config.Bind(
            "General",
            "PullThroughAllWards",
            false,
            new ConfigDescription(
                "When on, magnets also pull items that sit inside other players' wards. Players can always pull items inside their own wards.",
                null,
                ConfigHints.Synced(90)));
        Active = config.Bind(
            "Local",
            "Active",
            true,
            new ConfigDescription(
                "When off, your magnet does not extend pickup. Vanilla auto-pickup stays on. This client only.",
                null,
                ConfigHints.Local(200)));
        Toggle = config.Bind(
            "Local",
            "Toggle",
            new KeyboardShortcut(KeyCode.V, KeyCode.LeftAlt),
            new ConfigDescription(
                "Key that turns your magnet on or off. This client only.",
                null,
                ConfigHints.Local(190)));
        MaxHits = config.Bind(
            "Local",
            "MaxHits",
            2048,
            new ConfigDescription(
                "Most nearby colliders one magnet pass will search. The search starts at 128 and grows to this cap. Lower it if a large pull hitches. This client only.",
                new AcceptableValueRange<int>(128, 2048),
                ConfigHints.Local(180)));

        var tiers = new Dictionary<string, TierConfig>(StringComparer.Ordinal);
        for (var i = 0; i < MagnetCatalog.All.Count; i++)
        {
            var tier = MagnetCatalog.All[i];
            tiers[tier.Id] = new TierConfig(config, tier, 80 - (i * 10));
        }

        Tiers = tiers;
        Cast = new CastConfig(config);
    }

    public ConfigEntry<bool> Enabled { get; }

    public ConfigEntry<bool> PullThroughAllWards { get; }

    public ConfigEntry<bool> Active { get; }

    public ConfigEntry<KeyboardShortcut> Toggle { get; }

    public ConfigEntry<int> MaxHits { get; }

    public IReadOnlyDictionary<string, TierConfig> Tiers { get; }

    public CastConfig Cast { get; }

    public bool TryTier(string? prefabName, out TierConfig? tier)
    {
        tier = null;
        if (!MagnetCatalog.TryGet(prefabName, out var info) || info == null)
        {
            return false;
        }

        return Tiers.TryGetValue(info.Id, out tier);
    }
}

internal sealed class CastConfig
{
    public CastConfig(ConfigFile config)
    {
        const string section = MagnetCast.EnglishName;
        Craft = config.Bind(
            section,
            "Craft",
            MagnetCast.Craft,
            new ConfigDescription(
                "Ingredients for a magnet cast. Item prefab names and amounts, separated by commas.",
                null,
                ConfigHints.Synced(20)));
        Station = config.Bind(
            section,
            "Station",
            MagnetCast.Station,
            new ConfigDescription(
                "Crafting station prefab name.",
                null,
                ConfigHints.Synced(19)));
        StationLevel = config.Bind(
            section,
            "StationLevel",
            MagnetCast.StationLevel,
            new ConfigDescription(
                "Crafting station level required.",
                new AcceptableValueRange<int>(1, 7),
                ConfigHints.Synced(18)));
        Foundry = config.Bind(
            section,
            "Foundry",
            MagnetCast.Foundry,
            new ConfigDescription(
                "Frost foundry prefab. Place the cast in it and liquid frost in the fuel slot. Read when a world loads.",
                null,
                ConfigHints.Synced(17)));
        CookTime = config.Bind(
            section,
            "CookTime",
            MagnetCast.CookTime,
            new ConfigDescription(
                "Seconds the frost foundry takes to turn one cast into a bloodgold magnet.",
                new AcceptableValueRange<float>(1f, 300f),
                ConfigHints.Synced(16)));
    }

    public ConfigEntry<string> Craft { get; }

    public ConfigEntry<string> Station { get; }

    public ConfigEntry<int> StationLevel { get; }

    public ConfigEntry<string> Foundry { get; }

    public ConfigEntry<float> CookTime { get; }

    public void Listen(EventHandler handler)
    {
        Craft.SettingChanged += handler;
        Station.SettingChanged += handler;
        StationLevel.SettingChanged += handler;
        CookTime.SettingChanged += handler;
    }
}

static class ConfigHints
{
    public static SyncHint Synced(int order)
    {
        return new SyncHint { IsAdminOnly = true, Order = order };
    }

    public static SyncHint Local(int order)
    {
        return new SyncHint { IsAdminOnly = false, Order = order };
    }
}
