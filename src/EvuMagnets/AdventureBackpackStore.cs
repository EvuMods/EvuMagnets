using System;
using System.Reflection;
using UnityEngine;

namespace EvuMagnets;

/// <summary>
/// Adventure Backpacks stores on <c>Inventory.AddItem</c> when <c>ShouldStoreToBackpack</c> is true.
/// </summary>
sealed class AdventureBackpackStore : IExtraStore
{
    const BindingFlags StaticMethod = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool _resolved;
    static bool _logged;
    static MethodInfo? _shouldStore;

    public bool TryAccept(Player player, Inventory inventory, ItemDrop.ItemData item, out float weightFactor)
    {
        weightFactor = 1f;
        try
        {
            if (!Ensure() || _shouldStore == null)
            {
                return false;
            }

            var args = new object[] { player, item, null! };
            return (bool)_shouldStore.Invoke(null, args);
        }
        catch (Exception ex)
        {
            ExtraStores.LogOnce(ref _logged, "Adventure Backpacks store check failed: " + Message(ex));
            return false;
        }
    }

    static bool Ensure()
    {
        if (_resolved)
        {
            return _shouldStore != null;
        }

        _resolved = true;
        var type = ExtraStores.FindType("AdventureBackpacks.Features.StoreToBackpack");
        _shouldStore = type?.GetMethod(
            "ShouldStoreToBackpack",
            StaticMethod,
            null,
            new[] { typeof(Player), typeof(ItemDrop.ItemData), typeof(Inventory).MakeByRefType() },
            null);
        if (_shouldStore == null && ExtraStores.AssemblyLoaded("AdventureBackpacks"))
        {
            ExtraStores.LogOnce(
                ref _logged,
                "Adventure Backpacks is loaded, but its store check was not found. A full inventory will not pull into that backpack.");
        }

        return _shouldStore != null;
    }

    static string Message(Exception ex)
    {
        return ex.InnerException != null ? ex.InnerException.Message : ex.Message;
    }
}
