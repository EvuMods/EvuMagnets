using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

[HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
static class PickupPatch
{
    static readonly Collider[] Hits = new Collider[128];

    static void Prefix(Player __instance, ref float __state)
    {
        __state = __instance.m_autoPickupRange;
        if (!ShouldExtend(__instance, out var range))
        {
            return;
        }

        __instance.m_autoPickupRange = Plugin.Settings.PullThroughAllWards.Value
            ? range
            : PickupRules.VanillaRange;
    }

    static void Postfix(Player __instance, float dt, float __state)
    {
        var extend = ShouldExtend(__instance, out var range);
        var throughWards = Plugin.Settings != null && Plugin.Settings.PullThroughAllWards.Value;
        __instance.m_autoPickupRange = __state;
        if (extend && !throughWards)
        {
            PullExtraRing(__instance, dt, range);
        }
    }

    static bool ShouldExtend(Player player, out float range)
    {
        range = PickupRules.VanillaRange;
        if (player == null || Plugin.Settings == null || !Player.m_enableAutoPickup)
        {
            return false;
        }

        range = MagnetEquip.ActiveRange(player, Plugin.Settings);
        return range > PickupRules.VanillaRange;
    }

    static void PullExtraRing(Player player, float dt, float range)
    {
        if (player.IsDead() || player.IsTeleporting())
        {
            return;
        }

        var origin = player.transform.position + Vector3.up;
        var count = Physics.OverlapSphereNonAlloc(origin, range, Hits, player.m_autoPickupMask);
        var inventory = player.GetInventory();
        for (var i = 0; i < count; i++)
        {
            var collider = Hits[i];
            Hits[i] = null!;
            if (collider == null)
            {
                continue;
            }

            var body = collider.attachedRigidbody;
            if (body == null)
            {
                continue;
            }

            var drop = body.GetComponent<ItemDrop>();
            FloatingTerrainDummy? dummy = null;
            if (drop == null)
            {
                dummy = body.GetComponent<FloatingTerrainDummy>();
                if (dummy != null && dummy.m_parent != null)
                {
                    drop = dummy.m_parent.GetComponent<ItemDrop>();
                }
            }

            if (drop == null
                || !drop.m_autoPickup
                || drop.IsPiece()
                || player.HaveUniqueKey(drop.m_itemData.m_shared.m_name))
            {
                continue;
            }

            var view = drop.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                continue;
            }

            if (!drop.CanPickup(true))
            {
                drop.RequestOwn();
                continue;
            }

            if (drop.InTar())
            {
                continue;
            }

            var position = drop.transform.position;
            if (!PickupRules.AllowInWard(
                    Plugin.Settings.PullThroughAllWards.Value,
                    PrivateArea.CheckAccess(position, 0f, false, true)))
            {
                continue;
            }

            drop.Load();
            var data = drop.m_itemData;
            if (!inventory.CanAddItem(data, -1)
                || !PickupRules.FitsCarry(inventory.GetTotalWeight(), data.GetWeight(-1), player.GetMaxCarryWeight()))
            {
                continue;
            }

            var distance = Vector3.Distance(position, origin);
            if (!PickupRules.InExtraRing(distance, PickupRules.VanillaRange, range))
            {
                continue;
            }

            if (distance < PickupRules.PickupDistance)
            {
                player.Pickup(drop.gameObject, true, true);
                continue;
            }

            var step = (origin - position).normalized * (PickupRules.PullSpeed * dt);
            drop.transform.position += step;
            if (dummy != null)
            {
                dummy.transform.position += step;
            }
        }
    }
}
