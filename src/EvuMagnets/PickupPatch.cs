using System.Collections.Generic;
using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

[HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
static class PickupPatch
{
    static readonly Collider[] Hits = new Collider[128];
    static readonly List<PickupRules.ClaimDistance> Claims = new List<PickupRules.ClaimDistance>();

    static void Prefix(Player __instance, ref float __state)
    {
        __state = __instance.m_autoPickupRange;
        if (!ShouldExtend(__instance, out _))
        {
            return;
        }

        __instance.m_autoPickupRange = PickupRules.VanillaRange;
    }

    static void Postfix(Player __instance, float dt, float __state)
    {
        var extend = ShouldExtend(__instance, out var range);
        __instance.m_autoPickupRange = __state;
        if (extend)
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

            var playerPosition = player.transform.position;
            var horizontal = HorizontalDistance(position, playerPosition);
            var inReach = horizontal <= PickupRules.PickupDistance;
            var inRing = PickupRules.InExtraRing(horizontal, PickupRules.VanillaRange, range);
            if (!inReach && !inRing)
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

            FillClaims(player, position);
            var best = PickupRules.IsBestClaim(horizontal, player.GetPlayerID(), Claims, PickupRules.ClaimMargin);
            if (!view.IsOwner())
            {
                if (best && inRing)
                {
                    drop.RequestOwn();
                }

                continue;
            }

            if (!best || !drop.CanPickup())
            {
                continue;
            }

            if (inReach)
            {
                player.Pickup(drop.gameObject, true, true);
                continue;
            }

            PickupRules.PullOffset(
                position.x,
                position.z,
                playerPosition.x,
                playerPosition.z,
                PickupRules.PullSpeed,
                dt,
                out var offsetX,
                out var offsetZ);
            var step = new Vector3(offsetX, 0f, offsetZ);
            Stop(body);
            Stop(drop.GetComponent<Rigidbody>());
            drop.transform.position += step;
            if (dummy != null)
            {
                dummy.transform.position += step;
            }
        }
    }

    static void FillClaims(Player player, Vector3 itemPosition)
    {
        Claims.Clear();
        var players = Player.GetAllPlayers();
        for (var i = 0; i < players.Count; i++)
        {
            var other = players[i];
            if (other == null || ReferenceEquals(other, player))
            {
                continue;
            }

            Claims.Add(new PickupRules.ClaimDistance(
                HorizontalDistance(other.transform.position, itemPosition),
                other.GetPlayerID()));
        }
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.x - b.x;
        var dz = a.z - b.z;
        return Mathf.Sqrt((dx * dx) + (dz * dz));
    }

    static void Stop(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }
}
