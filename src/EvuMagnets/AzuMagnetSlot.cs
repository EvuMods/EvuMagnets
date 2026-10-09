#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using EvuMagnets.Core;
using UnityEngine;

namespace EvuMagnets;

internal static class AzuMagnetSlot
{
    const string SlotName = "Magnet";
    const string ApiTypeName = "AzuEPI.API, AzuExtendedPlayerInventory";
    static bool _loggedFailure;
    static Type _api;
    static CachedCall _slotIndex;
    static CachedCall _equippedItem;
    static CachedCall _gridPos;
    static int _cachedIndex = -1;

    public static bool Exists { get; private set; }

    public static bool Loaded
    {
        get
        {
            var api = Type.GetType(ApiTypeName);
            if (api == null)
            {
                return false;
            }

            var loaded = api.GetMethod("IsLoaded", BindingFlags.Public | BindingFlags.Static);
            return loaded != null && (bool)loaded.Invoke(null, null);
        }
    }

    public static bool TryGetFakeItemType(out ItemDrop.ItemData.ItemType itemType)
    {
        itemType = ItemDrop.ItemData.ItemType.Trinket;
        var api = Type.GetType(ApiTypeName);
        if (api == null)
        {
            return false;
        }

        var loaded = api.GetMethod("IsLoaded", BindingFlags.Public | BindingFlags.Static);
        if (loaded == null || !(bool)loaded.Invoke(null, null))
        {
            return false;
        }

        var method = api.GetMethod("GetFakeItemType", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return false;
        }

        itemType = (ItemDrop.ItemData.ItemType)method.Invoke(null, null);
        return true;
    }

    public static void Register(IReadOnlyList<string> prefabs)
    {
        Exists = false;
        _api = null;
        _cachedIndex = -1;
        var api = Type.GetType(ApiTypeName);
        if (api == null)
        {
            return;
        }

        var loaded = api.GetMethod("IsLoaded", BindingFlags.Public | BindingFlags.Static);
        if (loaded == null || !(bool)loaded.Invoke(null, null))
        {
            return;
        }

        var names = new string[prefabs.Count];
        for (var i = 0; i < prefabs.Count; i++)
        {
            names[i] = prefabs[i];
        }

        if (!TryAdd(api, names))
        {
            Plugin.Log.LogWarning("AzuEPI is loaded, but EvuMagnets could not add the Magnet slot.");
            return;
        }

        _api = api;
        _slotIndex = CachedCall.For(api, "TryGetSlotIndexByName");
        _equippedItem = CachedCall.For(api, "TryGetEquippedItem");
        _gridPos = CachedCall.For(api, "GetSlotGridPos");
        Exists = true;
        Plugin.Log.LogInfo("AzuEPI Magnet slot registered.");
    }

    public static bool TryGetEquippedPrefab(out string prefabName)
    {
        prefabName = null;
        if (!Exists || !TrySlotIndex(out var index))
        {
            return false;
        }

        if (!TryEquippedItem(index, out var item) || item == null)
        {
            return false;
        }

        prefabName = PrefabName(item);
        return PickupRules.IsMagnet(prefabName);
    }

    public static bool TryMoveToSlot(Player player, ItemDrop.ItemData item)
    {
        if (!Exists || player == null || item == null)
        {
            return false;
        }

        var inventory = player.GetInventory();
        if (inventory == null || !TryGetSlotGrid(inventory, out var x, out var y))
        {
            return false;
        }

        var occupant = inventory.GetItemAt(x, y);
        if (occupant != null && !ReferenceEquals(occupant, item))
        {
            return false;
        }

        if (item.m_gridPos.x == x && item.m_gridPos.y == y)
        {
            return true;
        }

        var moved = inventory.MoveItemToThis(inventory, item, item.m_stack, x, y);
        if (!moved && !_loggedFailure)
        {
            _loggedFailure = true;
            Plugin.Log.LogWarning("Could not move a magnet into the AzuEPI Magnet slot.");
        }

        return moved;
    }

    public static bool TryGetSlotGrid(Inventory inventory, out int x, out int y)
    {
        x = -1;
        y = -1;
        if (!Exists || inventory == null || !TrySlotIndex(out var index))
        {
            return false;
        }

        return TryGrid(inventory, index, out x, out y);
    }

    static bool TryAdd(Type api, string[] prefabs)
    {
        MethodInfo collection = null;
        MethodInfo single = null;
        foreach (var method in api.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "AddSlot")
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length < 2 || parameters[0].ParameterType != typeof(string))
            {
                continue;
            }

            var second = parameters[1].ParameterType;
            if (second == typeof(string))
            {
                single = method;
            }
            else if (second == typeof(string[]) || AcceptsStrings(second))
            {
                collection = method;
            }
        }

        if (collection != null)
        {
            return InvokeAdd(collection, prefabs);
        }

        if (single == null)
        {
            return false;
        }

        for (var i = 0; i < prefabs.Length; i++)
        {
            if (!InvokeAdd(single, prefabs[i]))
            {
                return false;
            }
        }

        return true;
    }

    static bool InvokeAdd(MethodInfo method, object prefabs)
    {
        var parameters = method.GetParameters();
        var args = new object[parameters.Length];
        args[0] = SlotName;
        args[1] = prefabs;
        for (var i = 2; i < parameters.Length; i++)
        {
            args[i] = parameters[i].ParameterType == typeof(int) ? -1 : Default(parameters[i].ParameterType);
        }

        var result = method.Invoke(null, args);
        return !(result is bool ok) || ok;
    }

    static bool TrySlotIndex(out int index)
    {
        index = _cachedIndex;
        if (index >= 0)
        {
            return true;
        }

        if (_api == null || _slotIndex == null)
        {
            return false;
        }

        var parameters = _slotIndex.Parameters;
        var args = _slotIndex.Args;
        args[0] = SlotName;
        for (var i = 1; i < parameters.Length; i++)
        {
            args[i] = Default(parameters[i].ParameterType);
        }

        var found = (bool)_slotIndex.Method.Invoke(null, args);
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(int).MakeByRefType())
            {
                index = (int)args[i];
            }
        }

        if (found && index >= 0)
        {
            _cachedIndex = index;
        }

        return found;
    }

    static bool TryEquippedItem(int index, out ItemDrop.ItemData item)
    {
        item = null;
        if (_equippedItem == null)
        {
            return false;
        }

        var parameters = _equippedItem.Parameters;
        var args = _equippedItem.Args;
        var itemSlot = -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            if (type == typeof(int))
            {
                args[i] = index;
            }
            else if (type.IsByRef && type.GetElementType() == typeof(ItemDrop.ItemData))
            {
                itemSlot = i;
                args[i] = null;
            }
            else
            {
                args[i] = Default(type);
            }
        }

        var found = (bool)_equippedItem.Method.Invoke(null, args);
        if (itemSlot >= 0)
        {
            item = args[itemSlot] as ItemDrop.ItemData;
            args[itemSlot] = null;
        }

        return found && item != null;
    }

    static bool TryGrid(Inventory inventory, int index, out int x, out int y)
    {
        x = -1;
        y = -1;
        if (_gridPos == null)
        {
            return false;
        }

        var parameters = _gridPos.Parameters;
        var args = _gridPos.Args;
        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            if (type == typeof(int))
            {
                args[i] = index;
            }
            else if (typeof(Inventory).IsAssignableFrom(type))
            {
                args[i] = inventory;
            }
            else
            {
                args[i] = Default(type);
            }
        }

        var result = _gridPos.Method.Invoke(null, args);
        var read = false;
        if (result != null)
        {
            var resultType = result.GetType();
            var xField = resultType.GetField("x");
            var yField = resultType.GetField("y");
            if (xField != null && yField != null)
            {
                x = Convert.ToInt32(xField.GetValue(result));
                y = Convert.ToInt32(yField.GetValue(result));
                read = true;
            }
        }

        if (!read)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(int).MakeByRefType())
                {
                    if (x < 0)
                    {
                        x = (int)args[i];
                    }
                    else
                    {
                        y = (int)args[i];
                    }
                }
            }
        }

        for (var i = 0; i < args.Length; i++)
        {
            args[i] = null;
        }

        // AzuEPI reports a cell past the last row when its equipment row is off.
        return x >= 0 && y >= 0 && x < inventory.GetWidth() && y < inventory.GetHeight();
    }

    static bool AcceptsStrings(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var arguments = type.GetGenericArguments();
        return arguments.Length == 1 && arguments[0] == typeof(string);
    }

    static object Default(Type type)
    {
        var actual = type.IsByRef ? type.GetElementType() : type;
        if (actual == typeof(string))
        {
            return "";
        }

        return actual.IsValueType ? Activator.CreateInstance(actual) : null;
    }

    static string PrefabName(ItemDrop.ItemData item)
    {
        if (item.m_dropPrefab != null)
        {
            return item.m_dropPrefab.name;
        }

        return null;
    }

    sealed class CachedCall
    {
        CachedCall(MethodInfo method)
        {
            Method = method;
            Parameters = method.GetParameters();
            Args = new object[Parameters.Length];
        }

        public MethodInfo Method { get; }

        public ParameterInfo[] Parameters { get; }

        public object[] Args { get; }

        public static CachedCall For(Type api, string name)
        {
            var method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            return method != null ? new CachedCall(method) : null;
        }
    }
}
