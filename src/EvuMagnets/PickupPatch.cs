using System;
using System.Collections.Generic;
using EvuMagnets.Core;
using HarmonyLib;
using UnityEngine;

namespace EvuMagnets;

[HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
static class PickupPatch
{
    const int InitialHits = 128;
    static Collider[] Hits = new Collider[InitialHits];
    static readonly List<PickupRules.ClaimDistance> Claims = new List<PickupRules.ClaimDistance>();
    static bool _loggedDropFailure;

    struct Pass
    {
        public float OriginalRange;
        public bool Extend;
        public float Range;
    }

    static void Prefix(Player __instance, ref Pass __state)
    {
        __state.OriginalRange = __instance.m_autoPickupRange;
        __state.Extend = ShouldExtend(__instance, out __state.Range);
        MagnetReach.Publish(__instance, __state.Extend ? __state.Range : 0f);
        if (__state.Extend)
        {
            __instance.m_autoPickupRange = PickupRules.VanillaRange;
        }
    }

    static void Postfix(Player __instance, float dt, Pass __state)
    {
        __instance.m_autoPickupRange = __state.OriginalRange;
        if (__state.Extend)
        {
            PullExtraRing(__instance, dt, __state.Range);
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
        var count = Overlap(origin, range, player.m_autoPickupMask);
        var inventory = player.GetInventory();
        for (var i = 0; i < count; i++)
        {
            var collider = Hits[i];
            Hits[i] = null!;
            if (collider == null)
            {
                continue;
            }

            try
            {
                PullOne(player, inventory, collider, origin, range, dt);
            }
            catch (Exception ex)
            {
                if (!_loggedDropFailure)
                {
                    _loggedDropFailure = true;
                    Plugin.Log.LogWarning("Skipped a drop the magnet could not read: " + ex.Message);
                }
            }
        }
    }

    static void PullOne(Player player, Inventory inventory, Collider collider, Vector3 origin, float range, float dt)
    {
        var body = collider.attachedRigidbody;
        if (body == null)
        {
            return;
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
            || drop.m_itemData == null
            || drop.m_itemData.m_shared == null
            || !drop.m_autoPickup
            || drop.IsPiece()
            || player.HaveUniqueKey(drop.m_itemData.m_shared.m_name))
        {
            return;
        }

        var view = drop.GetComponent<ZNetView>();
        if (view == null || !view.IsValid() || drop.InTar())
        {
            return;
        }

        var position = drop.transform.position;
        if (!PickupRules.AllowInWard(
                Plugin.Settings.PullThroughAllWards.Value,
                PrivateArea.CheckAccess(position, 0f, false, true)))
        {
            return;
        }

        var playerPosition = player.transform.position;
        var horizontal = HorizontalDistance(position, playerPosition);
        var vanillaDistance = Vector3.Distance(position, origin);
        var inReach = horizontal <= PickupRules.PickupDistance;
        var inRing = PickupRules.NeedsPull(vanillaDistance, PickupRules.VanillaRange, range, PickupRules.HandoffMargin);
        if (!inReach && !inRing)
        {
            return;
        }

        var data = drop.m_itemData;
        if (!inventory.CanAddItem(data, -1)
            || !PickupRules.FitsCarry(inventory.GetTotalWeight(), data.GetWeight(-1), player.GetMaxCarryWeight()))
        {
            return;
        }

        FillClaims(player, position);
        var best = PickupRules.IsBestClaim(horizontal, player.GetPlayerID(), Claims, PickupRules.ClaimMargin);
        if (!view.IsOwner())
        {
            if (best && inRing)
            {
                drop.RequestOwn();
            }

            return;
        }

        if (!best || !drop.CanPickup(true))
        {
            return;
        }

        // Vanilla reads the ZDO only once this client owns the drop.
        drop.Load();

        if (inReach)
        {
            player.Pickup(drop.gameObject, true, true);
            return;
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

    static int Overlap(Vector3 origin, float range, int mask)
    {
        var cap = HitCap();
        if (Hits.Length > cap)
        {
            Hits = new Collider[cap];
        }

        while (true)
        {
            var count = Physics.OverlapSphereNonAlloc(origin, range, Hits, mask);
            if (count < Hits.Length || Hits.Length >= cap)
            {
                return count;
            }

            var next = Hits.Length * 2;
            if (next > cap)
            {
                next = cap;
            }

            if (next <= Hits.Length)
            {
                return count;
            }

            Hits = new Collider[next];
        }
    }

    static int HitCap()
    {
        var value = Plugin.Settings != null ? Plugin.Settings.MaxHits.Value : 2048;
        if (value < 128)
        {
            return 128;
        }

        if (value > 2048)
        {
            return 2048;
        }

        return value;
    }

    static void FillClaims(Player player, Vector3 itemPosition)
    {
        Claims.Clear();
        var players = Player.GetAllPlayers();
        for (var i = 0; i < players.Count; i++)
        {
            var other = players[i];
            if (other == null || ReferenceEquals(other, player) || other.IsDead() || other.IsTeleporting())
            {
                continue;
            }

            // Only a player whose own reach covers the drop can take it, so only they count.
            var distance = HorizontalDistance(other.transform.position, itemPosition);
            if (!PickupRules.CanReach(distance, MagnetReach.Of(other)))
            {
                continue;
            }

            Claims.Add(new PickupRules.ClaimDistance(distance, other.GetPlayerID()));
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
