using System.Collections.Generic;
using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

internal static class MagnetEquip
{
    public static void OnEquipped(Player player, ItemDrop.ItemData item)
    {
        if (player == null || item == null || !PickupRules.IsMagnet(PrefabName(item)))
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

        player.SetupEquipment();
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
