using System;
using System.Collections.Generic;

namespace EvuMagnets.Core;

public static class PickupRules
{
    public const float VanillaRange = 2f;
    public const float PickupDistance = 0.3f;
    public const float PullSpeed = 15f;
    public const string PrefabPrefix = "evu_magnet_";

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
}
