using System;
using System.Collections.Generic;
using System.Reflection;
using EvuMagnets.Core;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace EvuMagnets;

internal static class MagnetItems
{
    static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>(StringComparer.Ordinal);
    static readonly Dictionary<string, ParsedTier> Parsed = new Dictionary<string, ParsedTier>(StringComparer.Ordinal);
    static PluginConfig _config = null!;
    static bool _registered;

    public static void Bind(PluginConfig config)
    {
        _config = config;
        foreach (var pair in config.Tiers)
        {
            pair.Value.Listen((_, __) => ApplyRecipes());
        }

        config.Cast.Listen((_, __) => ApplyRecipes());
    }

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        AddTranslations();
        var source = FindCloneSource();
        var names = new string[MagnetCatalog.All.Count];
        for (var i = 0; i < MagnetCatalog.All.Count; i++)
        {
            var tier = MagnetCatalog.All[i];
            names[i] = tier.PrefabName;
            RegisterTier(tier, source);
        }

        AzuMagnetSlot.Register(names);
        RegisterCast();
        RegisterConversion();
        ApplyRecipes();
    }

    public static void ApplyRecipes()
    {
        if (_config == null)
        {
            return;
        }

        Parsed.Clear();
        foreach (var tier in MagnetCatalog.All)
        {
            if (!_config.Tiers.TryGetValue(tier.Id, out var settings))
            {
                continue;
            }

            if (!tier.Forged
                || settings.Craft == null
                || settings.Upgrades == null
                || settings.Station == null
                || settings.StationLevel == null)
            {
                continue;
            }

            if (!RecipeText.TryParseList(settings.Craft.Value, out var craft, out var error)
                || !RecipeText.TryParseUpgrades(settings.Upgrades.Value, out var upgrades, out error))
            {
                Plugin.Log.LogWarning(tier.EnglishName + " recipe was not applied: " + error);
                continue;
            }

            Parsed[tier.PrefabName] = new ParsedTier(craft, upgrades);
            if (!Recipes.TryGetValue(tier.PrefabName, out var recipe) || recipe == null)
            {
                continue;
            }

            var station = FindStation(settings.Station.Value);
            if (station == null)
            {
                Plugin.Log.LogWarning(tier.EnglishName + " station was not found: " + settings.Station.Value);
            }
            else
            {
                recipe.m_craftingStation = station;
            }

            recipe.m_minStationLevel = settings.StationLevel.Value;
            if (!TryBuildRequirements(craft, out var requirements))
            {
                recipe.m_enabled = false;
                recipe.m_resources = Array.Empty<Piece.Requirement>();
                continue;
            }

            recipe.m_enabled = true;
            recipe.m_resources = requirements;
        }

        ApplyCast();
        ApplyFoundryTime();
    }

    public static bool TryAmount(object requirement, int quality, out int amount)
    {
        amount = 0;
        if (requirement == null)
        {
            return false;
        }

        var resItem = requirement.GetType().GetField("m_resItem")?.GetValue(requirement) as ItemDrop;
        if (resItem == null || string.IsNullOrEmpty(resItem.name))
        {
            return false;
        }

        var itemName = resItem.name;

        foreach (var pair in Recipes)
        {
            var resources = pair.Value != null ? pair.Value.m_resources : null;
            if (resources == null || !Parsed.TryGetValue(pair.Key, out var parsed))
            {
                continue;
            }

            for (var i = 0; i < resources.Length; i++)
            {
                if (!ReferenceEquals(resources[i], requirement))
                {
                    continue;
                }

                amount = RecipeText.AmountFor(quality, itemName, parsed.Craft, parsed.Upgrades);
                return true;
            }
        }

        return false;
    }

    static void RegisterTier(MagnetTierInfo tier, string source)
    {
        var prefab = PrefabManager.Instance.CreateClonedPrefab(tier.PrefabName, source);
        if (prefab == null)
        {
            Plugin.Log.LogError("Could not clone " + source + " for " + tier.PrefabName + ".");
            return;
        }

        var drop = prefab.GetComponent<ItemDrop>();
        var shared = drop.m_itemData.m_shared;
        shared.m_name = "$" + tier.Token;
        shared.m_description = "$" + tier.Token + "_desc";
        shared.m_itemType = ItemDrop.ItemData.ItemType.Trinket;
        shared.m_maxStackSize = 1;
        shared.m_weight = 2f;
        shared.m_maxQuality = tier.Ranges.Length;
        shared.m_teleportable = true;
        shared.m_useDurability = false;
        shared.m_durabilityDrain = 0f;
        shared.m_equipStatusEffect = null;
        shared.m_setStatusEffect = null;
        shared.m_setName = "";
        shared.m_setSize = 0;
        shared.m_skillType = Skills.SkillType.None;
        shared.m_icons = new[] { ItemIcons.Load(tier.Id) };
        drop.m_itemData.m_dropPrefab = prefab;
        Tint(prefab, tier.Id);
        if (!tier.Forged)
        {
            ItemManager.Instance.AddItem(new CustomItem(prefab, false));
            return;
        }

        var recipe = ScriptableObject.CreateInstance<Recipe>();
        recipe.name = "Recipe_" + tier.PrefabName;
        recipe.m_item = drop;
        recipe.m_amount = 1;
        recipe.m_enabled = true;
        var custom = new CustomItem(prefab, false);
        custom.Recipe = new CustomRecipe(recipe, false, false);
        ItemManager.Instance.AddItem(custom);
        Recipes[tier.PrefabName] = custom.Recipe.Recipe;
    }

    static void AddTranslations()
    {
        var localization = LocalizationManager.Instance.GetLocalization();
        for (var i = 0; i < MagnetCatalog.All.Count; i++)
        {
            var tier = MagnetCatalog.All[i];
            localization.AddTranslation("English", tier.Token, tier.EnglishName);
            localization.AddTranslation("English", tier.Token + "_desc", tier.EnglishDescription);
        }

        localization.AddTranslation("English", MagnetCast.Token, MagnetCast.EnglishName);
        localization.AddTranslation("English", MagnetCast.Token + "_desc", MagnetCast.EnglishDescription);
    }

    static void RegisterCast()
    {
        var prefab = PrefabManager.Instance.CreateClonedPrefab(MagnetCast.PrefabName, "Iron");
        if (prefab == null)
        {
            Plugin.Log.LogError("Could not clone Iron for " + MagnetCast.PrefabName + ".");
            return;
        }

        var drop = prefab.GetComponent<ItemDrop>();
        var shared = drop.m_itemData.m_shared;
        shared.m_name = "$" + MagnetCast.Token;
        shared.m_description = "$" + MagnetCast.Token + "_desc";
        shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
        shared.m_maxStackSize = 10;
        shared.m_weight = 2f;
        shared.m_maxQuality = 1;
        shared.m_teleportable = true;
        shared.m_useDurability = false;
        shared.m_durabilityDrain = 0f;
        shared.m_equipStatusEffect = null;
        shared.m_setStatusEffect = null;
        shared.m_setName = "";
        shared.m_setSize = 0;
        shared.m_skillType = Skills.SkillType.None;
        shared.m_icons = new[] { ItemIcons.Load(MagnetCast.Id) };
        drop.m_itemData.m_dropPrefab = prefab;
        Tint(prefab, MagnetCast.Id);

        var recipe = ScriptableObject.CreateInstance<Recipe>();
        recipe.name = "Recipe_" + MagnetCast.PrefabName;
        recipe.m_item = drop;
        recipe.m_amount = 1;
        recipe.m_enabled = true;
        var custom = new CustomItem(prefab, false);
        custom.Recipe = new CustomRecipe(recipe, false, false);
        ItemManager.Instance.AddItem(custom);
        Recipes[MagnetCast.PrefabName] = custom.Recipe.Recipe;
    }

    static void RegisterConversion()
    {
        ItemManager.Instance.AddItemConversion(new CustomItemConversion(new CookingConversionConfig
        {
            Station = _config.Cast.Foundry.Value,
            FromItem = MagnetCast.PrefabName,
            ToItem = MagnetCast.OutputPrefab,
            CookTime = _config.Cast.CookTime.Value,
        }));
    }

    static void ApplyFoundryTime()
    {
        var prefab = PrefabManager.Instance.GetPrefab(_config.Cast.Foundry.Value);
        var station = prefab != null ? prefab.GetComponent<CookingStation>() : null;
        if (station == null || station.m_conversion == null)
        {
            return;
        }

        for (var i = 0; i < station.m_conversion.Count; i++)
        {
            var conversion = station.m_conversion[i];
            if (conversion != null && conversion.m_from != null && conversion.m_from.name == MagnetCast.PrefabName)
            {
                conversion.m_cookTime = _config.Cast.CookTime.Value;
                return;
            }
        }
    }

    static void ApplyCast()
    {
        if (!Recipes.TryGetValue(MagnetCast.PrefabName, out var recipe) || recipe == null)
        {
            return;
        }

        if (!RecipeText.TryParseList(_config.Cast.Craft.Value, out var craft, out var error))
        {
            Plugin.Log.LogWarning("Magnet cast recipe was not applied: " + error);
            recipe.m_enabled = false;
            recipe.m_resources = Array.Empty<Piece.Requirement>();
            return;
        }

        var station = FindStation(_config.Cast.Station.Value);
        if (station == null)
        {
            Plugin.Log.LogWarning("Magnet cast station was not found: " + _config.Cast.Station.Value);
        }
        else
        {
            recipe.m_craftingStation = station;
        }

        recipe.m_minStationLevel = _config.Cast.StationLevel.Value;
        if (!TryBuildRequirements(craft, out var requirements))
        {
            recipe.m_enabled = false;
            recipe.m_resources = Array.Empty<Piece.Requirement>();
            return;
        }

        recipe.m_enabled = true;
        recipe.m_resources = requirements;
    }

    static string FindCloneSource()
    {
        var items = ObjectDB.instance != null ? ObjectDB.instance.m_items : null;
        if (items != null)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var drop = items[i] != null ? items[i].GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trinket)
                {
                    return items[i].name;
                }
            }
        }

        return "BeltStrength";
    }

    static CraftingStation? FindStation(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var prefab = PrefabManager.Instance.GetPrefab(name.Trim());
        return prefab != null ? prefab.GetComponent<CraftingStation>() : null;
    }

    static bool TryBuildRequirements(IReadOnlyList<Ingredient> craft, out Piece.Requirement[] requirements)
    {
        requirements = new Piece.Requirement[craft.Count];
        for (var i = 0; i < craft.Count; i++)
        {
            var drop = FindItem(craft[i].Item);
            if (drop == null)
            {
                Plugin.Log.LogWarning("Recipe item was not found: " + craft[i].Item);
                requirements = Array.Empty<Piece.Requirement>();
                return false;
            }

            var requirement = new Piece.Requirement
            {
                m_resItem = drop,
                m_amount = craft[i].Amount,
                m_amountPerLevel = 0,
                m_recover = true,
            };
            var upgrader = requirement.GetType().GetField("m_upgraderResource");
            if (upgrader != null && upgrader.FieldType == typeof(bool))
            {
                upgrader.SetValue(requirement, false);
            }

            requirements[i] = requirement;
        }

        return true;
    }

    static ItemDrop? FindItem(string name)
    {
        // OnVanillaItemsAvailable runs before ObjectDB rebuilds m_itemByHash, so GetItemPrefab is empty there.
        var prefab = PrefabManager.Instance.GetPrefab(name);
        var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop != null)
        {
            return drop;
        }

        if (ObjectDB.instance == null)
        {
            return null;
        }

        var fromHash = ObjectDB.instance.GetItemPrefab(name);
        drop = fromHash != null ? fromHash.GetComponent<ItemDrop>() : null;
        if (drop != null)
        {
            return drop;
        }

        var items = ObjectDB.instance.m_items;
        if (items == null)
        {
            return null;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] == null || items[i].name != name)
            {
                continue;
            }

            drop = items[i].GetComponent<ItemDrop>();
            if (drop != null)
            {
                return drop;
            }
        }

        return null;
    }

    static void Tint(GameObject prefab, string id)
    {
        var color = id == "silver" ? new Color(0.78f, 0.82f, 0.86f)
            : id == "blackmetal" ? new Color(0.12f, 0.42f, 0.18f)
            : id == "flametal" ? new Color(0.95f, 0.28f, 0.05f)
            : id == "bloodgold" ? new Color(0.95f, 0.74f, 0.22f)
            : id == MagnetCast.Id ? new Color(0.78f, 0.74f, 0.62f)
            : new Color(0.45f, 0.42f, 0.40f);
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var materials = renderers[i].materials;
            for (var m = 0; m < materials.Length; m++)
            {
                if (materials[m] != null && materials[m].HasProperty("_Color"))
                {
                    materials[m].color = color;
                }
            }

            renderers[i].materials = materials;
        }
    }

    sealed class ParsedTier
    {
        public ParsedTier(IReadOnlyList<Ingredient> craft, IReadOnlyList<IReadOnlyList<Ingredient>> upgrades)
        {
            Craft = craft;
            Upgrades = upgrades;
        }

        public IReadOnlyList<Ingredient> Craft { get; }

        public IReadOnlyList<IReadOnlyList<Ingredient>> Upgrades { get; }
    }
}

[HarmonyPatch]
static class RequirementAmountPatch
{
    static MethodBase TargetMethod()
    {
        Type[] types;
        try
        {
            types = typeof(Recipe).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            if (type == null || type.Name != "Requirement")
            {
                continue;
            }

            var method = AccessTools.DeclaredMethod(type, "GetAmount", new[] { typeof(int) });
            if (method != null)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Requirement.GetAmount(int) was not found.");
    }

    static void Postfix(object __instance, int qualityLevel, ref int __result)
    {
        if (MagnetItems.TryAmount(__instance, qualityLevel, out var amount))
        {
            __result = amount;
        }
    }
}
