using System;
using System.Reflection;
using UnityEngine;

namespace EvuMagnets;

readonly struct StoreHit
{
    public StoreHit(bool wouldStore, float weightFactor)
    {
        WouldStore = wouldStore;
        WeightFactor = weightFactor;
    }

    public bool WouldStore { get; }

    public float WeightFactor { get; }

    public static StoreHit None => new StoreHit(false, 1f);
}

interface IExtraStore
{
    bool TryAccept(Player player, Inventory inventory, ItemDrop.ItemData item, out float weightFactor);
}

/// <summary>
/// Bags that store a drop during pickup. A new bag is a class added to <see cref="Stores"/>.
/// </summary>
static class ExtraStores
{
    static readonly IExtraStore[] Stores =
    {
        new AdventureBackpackStore(),
        new SmoothbrainBackpackStore(),
        new JewelcraftingGemBagStore(),
    };

    public static StoreHit Query(Player player, Inventory inventory, ItemDrop.ItemData item)
    {
        var hits = 0;
        var factor = 1f;
        for (var i = 0; i < Stores.Length; i++)
        {
            if (!Stores[i].TryAccept(player, inventory, item, out var storeFactor))
            {
                continue;
            }

            hits++;
            factor = storeFactor;
        }

        if (hits == 0)
        {
            return StoreHit.None;
        }

        if (hits > 1 || float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0f)
        {
            factor = 1f;
        }

        return new StoreHit(true, factor);
    }

    internal static Type? FindType(string fullName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var type = assemblies[i].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    internal static bool AssemblyLoaded(string simpleName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            if (string.Equals(assemblies[i].GetName().Name, simpleName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static void LogOnce(ref bool logged, string message)
    {
        if (logged)
        {
            return;
        }

        logged = true;
        Plugin.Log?.LogWarning(message);
    }
}
