using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace EvuMagnets;

/// <summary>
/// Smoothbrain Backpacks stores the whole stack from <c>Humanoid.Pickup</c> when Auto Fill is on.
/// Puddipacks, Storage Backpacks, and the EpicLoot Enchanting Pouch are bags of this kind.
/// <c>CanAddItem</c> is their item filter.
/// </summary>
sealed class SmoothbrainBackpackStore : IExtraStore
{
    const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool _resolved;
    static bool _logged;
    static MethodInfo? _data;
    static MethodInfo? _get;
    static MethodInfo? _mayAutoPickup;
    static MethodInfo? _canAddItem;
    static MethodInfo? _weightFactor;
    static MethodInfo? _canAddToInventory;
    static MethodInfo? _containsName;
    static FieldInfo? _inventory;
    static PropertyInfo? _item;
    static Type? _containerType;
    static Type? _customType;
    static FieldInfo? _allowedItems;

    public bool TryAccept(Player player, Inventory inventory, ItemDrop.ItemData item, out float weightFactor)
    {
        weightFactor = 1f;
        try
        {
            if (!Ensure() || inventory.m_inventory == null || _data == null || _get == null)
            {
                return false;
            }

            object? best = null;
            var bestScore = long.MinValue;
            var items = inventory.m_inventory;
            for (var i = 0; i < items.Count; i++)
            {
                var held = items[i];
                if (held == null || !TryContainer(held, out var container) || container == null)
                {
                    continue;
                }

                if (!CallBool(_mayAutoPickup, container, item) || !CallBool(_canAddItem, container, item))
                {
                    continue;
                }

                var bag = _inventory?.GetValue(container) as Inventory;
                if (bag == null || _canAddToInventory == null)
                {
                    continue;
                }

                if (!(bool)_canAddToInventory.Invoke(bag, new object[] { item, -1 }))
                {
                    continue;
                }

                var score = Score(container, bag, item.m_shared.m_name);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = container;
                }
            }

            if (best == null || _weightFactor == null)
            {
                return false;
            }

            weightFactor = (float)_weightFactor.Invoke(best, null);
            return true;
        }
        catch (Exception ex)
        {
            ExtraStores.LogOnce(ref _logged, "Backpacks store check failed: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            return false;
        }
    }

    bool TryContainer(ItemDrop.ItemData held, out object? container)
    {
        container = null;
        var info = _data!.Invoke(null, new object[] { held });
        if (info == null || _get == null || _containerType == null)
        {
            return false;
        }

        container = _get.Invoke(info, new object[] { string.Empty });
        return container != null;
    }

    static bool CallBool(MethodInfo? method, object instance, ItemDrop.ItemData item)
    {
        return method != null && (bool)method.Invoke(instance, new object[] { item });
    }

    long Score(object container, Inventory bag, string? itemName)
    {
        var held = _item?.GetValue(container, null) as ItemDrop.ItemData;
        var grid = held != null ? held.m_gridPos : default;
        long score = 1 + grid.x + (grid.y * 100L);
        if (itemName != null
            && _containsName != null
            && (bool)_containsName.Invoke(bag, new object[] { itemName }))
        {
            score += 10000L;
        }

        var type = container.GetType();
        if (_customType != null && !_customType.IsInstanceOfType(container) && type != _containerType)
        {
            score *= 400000000L;
        }
        else if (_customType != null && _customType.IsInstanceOfType(container) && HasAllowedList(held))
        {
            score *= 20000L;
        }

        return score;
    }

    bool HasAllowedList(ItemDrop.ItemData? backpack)
    {
        var name = backpack?.m_shared?.m_name;
        if (name == null || _allowedItems?.GetValue(null) is not IDictionary allowed || !allowed.Contains(name))
        {
            return false;
        }

        return allowed[name] is ICollection list && list.Count > 0;
    }

    static bool Ensure()
    {
        if (_resolved)
        {
            return _data != null;
        }

        _resolved = true;
        _containerType = ExtraStores.FindType("Backpacks.ItemContainer");
        _customType = ExtraStores.FindType("Backpacks.CustomBackpack");
        var extensions = ExtraStores.FindType("ItemDataManager.ItemExtensions");
        var infoType = ExtraStores.FindType("ItemDataManager.ItemInfo");
        _data = extensions?.GetMethod("Data", StaticAny, null, new[] { typeof(ItemDrop.ItemData) }, null);
        _get = GenericGet(infoType);
        if (_get != null && _containerType != null)
        {
            _get = _get.MakeGenericMethod(_containerType);
        }

        _mayAutoPickup = _containerType?.GetMethod("MayAutoPickup", InstanceAny, null, new[] { typeof(ItemDrop.ItemData) }, null);
        _canAddItem = _containerType?.GetMethod("CanAddItem", InstanceAny, null, new[] { typeof(ItemDrop.ItemData) }, null);
        _weightFactor = _containerType?.GetMethod("WeightFactor", InstanceAny, null, Type.EmptyTypes, null);
        _inventory = _containerType?.GetField("Inventory", InstanceAny);
        _item = _containerType?.GetProperty("Item", InstanceAny);
        _allowedItems = _customType?.GetField("AllowedItems", StaticAny);
        _canAddToInventory = typeof(Inventory).GetMethod(
            "CanAddItem",
            InstanceAny,
            null,
            new[] { typeof(ItemDrop.ItemData), typeof(int) },
            null);
        _containsName = typeof(Inventory).GetMethod(
            "ContainsItemByName",
            InstanceAny,
            null,
            new[] { typeof(string) },
            null);
        var ready = _data != null
            && _get != null
            && _mayAutoPickup != null
            && _canAddItem != null
            && _weightFactor != null
            && _inventory != null
            && _canAddToInventory != null;
        if (!ready && ExtraStores.AssemblyLoaded("Backpacks"))
        {
            ExtraStores.LogOnce(
                ref _logged,
                "Backpacks is loaded, but its auto-fill check was not found. A full inventory will not pull into those backpacks.");
        }

        return ready;
    }

    static MethodInfo? GenericGet(Type? infoType)
    {
        if (infoType == null)
        {
            return null;
        }

        var methods = infoType.GetMethods(InstanceAny);
        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (method.Name != "Get" || !method.IsGenericMethodDefinition)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                return method;
            }
        }

        return null;
    }
}
