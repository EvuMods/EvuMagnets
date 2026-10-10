using System;
using System.Reflection;
using UnityEngine;

namespace EvuMagnets;

/// <summary>
/// Jewelcrafting's gem bag probe. <c>dryRun: true</c> is the same call its pickup check uses, and it does not write the bag.
/// </summary>
sealed class JewelcraftingGemBagStore : IExtraStore
{
    const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static bool _resolved;
    static bool _logged;
    static MethodInfo? _dryRun;

    public bool TryAccept(Player player, Inventory inventory, ItemDrop.ItemData item, out float weightFactor)
    {
        weightFactor = 1f;
        try
        {
            if (!Ensure() || _dryRun == null)
            {
                return false;
            }

            return (bool)_dryRun.Invoke(null, new object[] { inventory, item, true });
        }
        catch (Exception ex)
        {
            ExtraStores.LogOnce(ref _logged, "Jewelcrafting gem bag check failed: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            return false;
        }
    }

    static bool Ensure()
    {
        if (_resolved)
        {
            return _dryRun != null;
        }

        _resolved = true;
        var setup = ExtraStores.FindType("Jewelcrafting.MiscSetup");
        var bag = setup?.GetNestedType("AddItemToBag", Any);
        _dryRun = bag?.GetMethod(
            "Do",
            Any,
            null,
            new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool) },
            null);
        if (_dryRun == null && ExtraStores.AssemblyLoaded("Jewelcrafting"))
        {
            ExtraStores.LogOnce(
                ref _logged,
                "Jewelcrafting is loaded, but its gem bag check was not found. A full inventory will not pull into that bag.");
        }

        return _dryRun != null;
    }
}
