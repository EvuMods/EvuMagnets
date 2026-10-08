using System.Collections.Generic;
using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

internal static class MagnetEquip
{
    static readonly List<string> EquippedNames = new List<string>();
    static int _reconciledId;
    static bool _loggedNoCell;

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

        _reconciledId = id;
        var inventory = player.GetInventory();
        if (inventory == null)
        {
            return;
        }

        var magnet = DedupeEquipped(player, inventory);
        if (magnet == null)
        {
            return;
        }

        if (!AzuMagnetSlot.TryGetSlotGrid(inventory, out var slotX, out var slotY))
        {
            if (!_loggedNoCell)
            {
                _loggedNoCell = true;
                Plugin.Log.LogInfo("Magnet slot has no grid cell; leaving the magnet where it is.");
            }

            return;
        }

        PlaceEquippedMagnet(inventory, magnet, slotX, slotY);
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

        UnequipOtherMagnets(player, item);
    }

    static void UnequipOtherMagnets(Player player, ItemDrop.ItemData keep)
    {
        var equipped = player.GetInventory().GetEquippedItems();
        for (var i = 0; i < equipped.Count; i++)
        {
            var other = equipped[i];
            if (other == null || ReferenceEquals(other, keep) || !PickupRules.IsMagnet(PrefabName(other)))
            {
                continue;
            }

            player.UnequipItem(other, false);
        }
    }

    static ItemDrop.ItemData? DedupeEquipped(Player player, Inventory inventory)
    {
        ItemDrop.ItemData? keep = null;
        var count = 0;
        var items = inventory.GetAllItems();
        AzuMagnetSlot.TryGetEquippedPrefab(out var slotPrefab);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || !item.m_equipped || !PickupRules.IsMagnet(PrefabName(item)))
            {
                continue;
            }

            count++;
            if (keep == null || (slotPrefab != null && PrefabName(item) == slotPrefab))
            {
                keep = item;
            }
        }

        if (count > 1 && keep != null)
        {
            UnequipOtherMagnets(player, keep);
        }

        return keep;
    }

    static void PlaceEquippedMagnet(Inventory inventory, ItemDrop.ItemData magnet, int slotX, int slotY)
    {
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
            return;
        }

        var occupant = inventory.GetItemAt(slotX, slotY);
        if (occupant != null && !ReferenceEquals(occupant, magnet))
        {
            occupant.m_gridPos = new Vector2i(occupantToX, occupantToY);
        }

        magnet.m_gridPos = new Vector2i(magnetToX, magnetToY);
        inventory.Changed();
    }

    public static string? ActivePrefab(Player? player)
    {
        if (player == null)
        {
            return null;
        }

        var equipped = player.GetInventory().GetEquippedItems();
        EquippedNames.Clear();
        for (var i = 0; i < equipped.Count; i++)
        {
            var name = PrefabName(equipped[i]);
            if (name != null)
            {
                EquippedNames.Add(name);
            }
        }

        AzuMagnetSlot.TryGetEquippedPrefab(out var slotPrefab);
        return PickupRules.ActivePrefab(AzuMagnetSlot.Exists, slotPrefab, EquippedNames);
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
    static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
    {
        // A refused equip (swimming, attacking, dodging) returns false. Mods that allow
        // equipping in those states return true, so this follows them without a check of our own.
        if (__result && __instance is Player player)
        {
            MagnetEquip.OnEquipped(player, item);
        }
    }
}
