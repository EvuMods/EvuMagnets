using System.Collections.Generic;
using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

internal static class MagnetEquip
{
    static int _reconciledId;

    public static void ReconcileAfterLoad(Player? player)
    {
        if (player == null || player.m_isLoading || !AzuMagnetSlot.Exists)
        {
            return;
        }

        var id = player.GetInstanceID();
        if (_reconciledId == id)
        {
            return;
        }

        if (!PlaceEquippedMagnet(player))
        {
            return;
        }

        _reconciledId = id;
    }

    public static void OnEquipped(Player player, ItemDrop.ItemData item)
    {
        if (player == null || item == null || player.m_isLoading || !PickupRules.IsMagnet(PrefabName(item)))
        {
            return;
        }

        if (AzuMagnetSlot.Exists)
        {
            AzuMagnetSlot.TryMoveToSlot(player, item);
        }

        var inventory = player.GetInventory();
        var equipped = inventory.GetEquippedItems();
        for (var i = 0; i < equipped.Count; i++)
        {
            var other = equipped[i];
            if (other == null || ReferenceEquals(other, item) || !PickupRules.IsMagnet(PrefabName(other)))
            {
                continue;
            }

            player.UnequipItem(other, false);
        }
    }

    static bool PlaceEquippedMagnet(Player player)
    {
        var inventory = player.GetInventory();
        if (inventory == null || !AzuMagnetSlot.TryGetSlotGrid(inventory, out var slotX, out var slotY))
        {
            return false;
        }

        var magnet = EquippedMagnet(inventory);
        if (magnet == null)
        {
            return true;
        }

        if (!PickupRules.TryExchangeMagnetSlot(
            magnet.m_equipped,
            magnet.m_gridPos.x,
            magnet.m_gridPos.y,
            slotX,
            slotY,
            out var magnetToX,
            out var magnetToY,
            out var occupantToX,
            out var occupantToY))
        {
            return true;
        }

        var occupant = inventory.GetItemAt(slotX, slotY);
        if (occupant != null && !ReferenceEquals(occupant, magnet))
        {
            occupant.m_gridPos = new Vector2i(occupantToX, occupantToY);
        }

        magnet.m_gridPos = new Vector2i(magnetToX, magnetToY);
        magnet.m_equipped = true;
        return true;
    }

    static ItemDrop.ItemData? EquippedMagnet(Inventory inventory)
    {
        var items = inventory.GetAllItems();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.m_equipped && PickupRules.IsMagnet(PrefabName(item)))
            {
                return item;
            }
        }

        return null;
    }

    public static string? ActivePrefab(Player? player)
    {
        if (player == null)
        {
            return null;
        }

        var equipped = player.GetInventory().GetEquippedItems();
        var names = new List<string>(equipped.Count);
        for (var i = 0; i < equipped.Count; i++)
        {
            var name = PrefabName(equipped[i]);
            if (name != null)
            {
                names.Add(name);
            }
        }

        AzuMagnetSlot.TryGetEquippedPrefab(out var slotPrefab);
        return PickupRules.ActivePrefab(AzuMagnetSlot.Exists, slotPrefab, names);
    }

    public static float ActiveRange(Player player, PluginConfig config)
    {
        if (config == null || !config.Enabled.Value || !config.Active.Value)
        {
            return PickupRules.VanillaRange;
        }

        var prefab = ActivePrefab(player);
        if (prefab == null || !config.TryTier(prefab, out var tier) || tier == null)
        {
            return PickupRules.VanillaRange;
        }

        var quality = 1;
        var equipped = player.GetInventory().GetEquippedItems();
        for (var i = 0; i < equipped.Count; i++)
        {
            if (PrefabName(equipped[i]) == prefab)
            {
                quality = equipped[i].m_quality;
                break;
            }
        }

        return PickupRules.RangeForQuality(tier.Ranges, quality);
    }

    public static string? PrefabName(ItemDrop.ItemData? item)
    {
        if (item == null || item.m_dropPrefab == null)
        {
            return null;
        }

        return item.m_dropPrefab.name;
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
static class EquipPatch
{
    [HarmonyPriority(Priority.Last)]
    static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (__instance is Player player)
        {
            MagnetEquip.OnEquipped(player, item);
        }
    }
}
