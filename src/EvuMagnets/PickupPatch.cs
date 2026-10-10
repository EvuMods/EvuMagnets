using System;
using System.Collections.Generic;
using System.Reflection;
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
    static readonly HashSet<string> Blocked = new HashSet<string>(StringComparer.Ordinal);
    static bool _loggedDropFailure;
    static bool _resolvedFreeCells;
    static MethodInfo? _freeNormalCells;

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
            // Vanilla would slide a drop, then say the inventory is full, when a quick slot is empty.
            __instance.m_autoPickupRange = 0f;
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

        Blocked.Clear();
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
        var inReach = vanillaDistance < PickupRules.VanillaPickupDistance;
        var inRing = vanillaDistance >= PickupRules.VanillaPickupDistance && vanillaDistance <= range;
        if (!inReach && !inRing)
        {
            return;
        }

        var data = drop.m_itemData;
        var itemName = data.m_shared.m_name;
        if (itemName != null && Blocked.Contains(itemName))
        {
            return;
        }

        // Vanilla reads the ZDO only once this client owns the drop.
        if (view.IsOwner())
        {
            drop.Load();
        }

        var stack = data.m_stack;
        var carried = inventory.GetTotalWeight();
        var maxCarry = player.GetMaxCarryWeight();
        if (!PickupRules.ShouldMove(1, FitsInventory(inventory, data, 1), carried, data.GetWeight(1), maxCarry))
        {
            if (itemName != null)
            {
                Blocked.Add(itemName);
            }

            return;
        }

        if (stack <= 0
            || !PickupRules.ShouldMove(stack, FitsInventory(inventory, data, stack), carried, data.GetWeight(stack), maxCarry))
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

        if (inReach)
        {
            player.Pickup(drop.gameObject, true, true);
            return;
        }

        PickupRules.PullOffset(
            position.x,
            position.y,
            position.z,
            origin.x,
            origin.y,
            origin.z,
            PickupRules.PullSpeed,
            dt,
            out var offsetX,
            out var offsetY,
            out var offsetZ);
        var step = new Vector3(offsetX, offsetY, offsetZ);
        Stop(body);
        Stop(drop.GetComponent<Rigidbody>());
        drop.transform.position += step;
        if (dummy != null)
        {
            dummy.transform.position += step;
        }
    }

    static bool FitsInventory(Inventory inventory, ItemDrop.ItemData data, int stack)
    {
        var shared = data.m_shared;
        var freeStack = inventory.FindFreeStackSpace(shared.m_name, data.m_worldLevel);
        return PickupRules.GridFits(stack, freeStack, FreeCells(inventory), shared.m_maxStackSize);
    }

    static int FreeCells(Inventory inventory)
    {
        if (!_resolvedFreeCells)
        {
            _resolvedFreeCells = true;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType("AzuEPI.Core.InventoryHandlers.Capacity");
                _freeNormalCells = type?.GetMethod(
                    "FreeNormalCells",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (_freeNormalCells != null)
                {
                    break;
                }
            }
        }

        if (_freeNormalCells != null)
        {
            return (int)_freeNormalCells.Invoke(null, new object[] { inventory });
        }

        return inventory.GetEmptySlots();
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
