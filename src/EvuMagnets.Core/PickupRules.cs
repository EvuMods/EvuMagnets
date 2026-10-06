using System;
using System.Collections.Generic;

namespace EvuMagnets.Core;

public static class PickupRules
{
    public const float VanillaRange = 2f;
    public const float PickupDistance = 1f;
    public const float HandoffMargin = 0.5f;
    public const float ClaimMargin = 0.75f;
    public const float PullSpeed = 15f;
    public const string PrefabPrefix = "evu_magnet_";

    public readonly struct ClaimDistance
    {
        public ClaimDistance(float distance, long playerId)
        {
            Distance = distance;
            PlayerId = playerId;
        }

        public float Distance { get; }

        public long PlayerId { get; }
    }

    public static bool IsMagnet(string? prefabName)
    {
        return prefabName != null
            && prefabName.StartsWith(PrefabPrefix, StringComparison.Ordinal);
    }

    public static float RangeForQuality(IReadOnlyList<float> radii, int quality)
    {
        if (radii == null || radii.Count == 0)
        {
            return VanillaRange;
        }

        var index = quality - 1;
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= radii.Count)
        {
            index = radii.Count - 1;
        }

        return radii[index];
    }

    public static bool FitsCarry(float carried, float incoming, float maxCarry)
    {
        return incoming >= 0f && carried + incoming <= maxCarry;
    }

    public static bool InExtraRing(float distance, float vanillaRange, float magnetRange)
    {
        return distance > vanillaRange && distance <= magnetRange;
    }

    public static bool NeedsPull(float distanceToOrigin, float vanillaRange, float magnetRange, float handoff)
    {
        var inner = vanillaRange - handoff;
        if (inner < 0f)
        {
            inner = 0f;
        }

        return distanceToOrigin > inner && distanceToOrigin <= magnetRange;
    }

    public static bool AllowInWard(bool pullThroughAllWards, bool playerHasAccess)
    {
        return pullThroughAllWards || playerHasAccess;
    }

    public static string? ActivePrefab(bool slotExists, string? slotPrefab, IReadOnlyList<string>? equippedPrefabs)
    {
        var magnets = new List<string>();
        if (equippedPrefabs != null)
        {
            for (var i = 0; i < equippedPrefabs.Count; i++)
            {
                if (IsMagnet(equippedPrefabs[i]))
                {
                    magnets.Add(equippedPrefabs[i]);
                }
            }
        }

        if (slotExists)
        {
            if (slotPrefab == null || !IsMagnet(slotPrefab))
            {
                return null;
            }

            var listed = false;
            for (var i = 0; i < magnets.Count; i++)
            {
                if (string.Equals(magnets[i], slotPrefab, StringComparison.Ordinal))
                {
                    listed = true;
                    break;
                }
            }

            if (!listed)
            {
                magnets.Add(slotPrefab);
            }

            if (magnets.Count != 1)
            {
                return null;
            }

            return slotPrefab;
        }

        if (magnets.Count != 1)
        {
            return null;
        }

        return magnets[0];
    }

    public static bool IsBestClaim(float myDistance, long myId, IReadOnlyList<ClaimDistance> others, float margin)
    {
        if (others == null)
        {
            return true;
        }

        for (var i = 0; i < others.Count; i++)
        {
            var other = others[i];
            var difference = other.Distance - myDistance;
            if (difference < -margin)
            {
                return false;
            }

            if (difference <= margin && other.PlayerId < myId)
            {
                return false;
            }
        }

        return true;
    }

    public static void PullOffset(
        float itemX,
        float itemZ,
        float playerX,
        float playerZ,
        float speed,
        float dt,
        out float offsetX,
        out float offsetZ)
    {
        var deltaX = playerX - itemX;
        var deltaZ = playerZ - itemZ;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        if (distance < 0.0001d || speed <= 0f || dt <= 0f)
        {
            offsetX = 0f;
            offsetZ = 0f;
            return;
        }

        var step = (double)speed * dt;
        if (distance <= step)
        {
            offsetX = (float)deltaX;
            offsetZ = (float)deltaZ;
            return;
        }

        var scale = step / distance;
        offsetX = (float)(deltaX * scale);
        offsetZ = (float)(deltaZ * scale);
    }
}
