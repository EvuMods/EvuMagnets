using System;
using System.Reflection;
using AdventureBackpacks.API.Client;

namespace EvuMagnets;

/// <summary>
/// Adventure Backpacks stores on <c>Inventory.AddItem</c> when <c>ABAPI.WouldStorePickup</c> is true.
/// That query needs Adventure Backpacks 2.4.0 or newer.
/// </summary>
sealed class AdventureBackpackStore : IExtraStore
{
    const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    static bool _resolved;
    static bool _logged;
    static bool _hasQuery;

    public bool TryAccept(Player player, Inventory inventory, ItemDrop.ItemData item, out float weightFactor)
    {
        weightFactor = 1f;
        try
        {
            if (!Ensure())
            {
                return false;
            }

            return ABAPIClient.WouldStorePickup(player, item);
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
            return _hasQuery;
        }

        _resolved = true;
        var type = ExtraStores.FindType("AdventureBackpacks.API.ABAPI");
        _hasQuery = type?.GetMethod(
            "WouldStorePickup",
            PublicStatic,
            null,
            new[] { typeof(Player), typeof(ItemDrop.ItemData) },
            null) != null;
        if (!_hasQuery && ExtraStores.AssemblyLoaded("AdventureBackpacks"))
        {
            ExtraStores.LogOnce(
                ref _logged,
                "Adventure Backpacks is loaded, but WouldStorePickup was not found. Adventure Backpacks 2.4.0 or newer is required. A full inventory will not pull into that backpack.");
        }

        return _hasQuery;
    }

    static string Message(Exception ex)
    {
        return ex.InnerException != null ? ex.InnerException.Message : ex.Message;
    }
}
