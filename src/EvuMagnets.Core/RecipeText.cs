using System;
using System.Collections.Generic;

namespace EvuMagnets.Core;

public static class RecipeText
{
    public static bool TryParseList(string text, out IReadOnlyList<Ingredient> items, out string error)
    {
        items = Array.Empty<Ingredient>();
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Recipe is empty.";
            return false;
        }

        var parts = text.Split(',');
        var parsed = new List<Ingredient>(parts.Length);
        foreach (var part in parts)
        {
            if (!TryParseOne(part, out var ingredient, out error))
            {
                return false;
            }

            parsed.Add(ingredient);
        }

        items = parsed;
        return true;
    }

    public static bool TryParseUpgrades(string text, out IReadOnlyList<IReadOnlyList<Ingredient>> steps, out string error)
    {
        steps = Array.Empty<IReadOnlyList<Ingredient>>();
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Upgrades are empty.";
            return false;
        }

        var groups = text.Split(';');
        if (groups.Length != 3)
        {
            error = "Upgrades need three groups, separated by semicolons.";
            return false;
        }

        var parsed = new List<IReadOnlyList<Ingredient>>(3);
        foreach (var group in groups)
        {
            if (!TryParseList(group, out var items, out error))
            {
                return false;
            }

            parsed.Add(items);
        }

        steps = parsed;
        return true;
    }

    public static int AmountFor(int quality, string item, IReadOnlyList<Ingredient> craft, IReadOnlyList<IReadOnlyList<Ingredient>> upgrades)
    {
        if (string.IsNullOrEmpty(item))
        {
            return 0;
        }

        if (quality <= 1)
        {
            return Sum(craft, item);
        }

        var step = quality - 2;
        if (upgrades == null || step < 0 || step >= upgrades.Count)
        {
            return 0;
        }

        return Sum(upgrades[step], item);
    }

    static int Sum(IReadOnlyList<Ingredient> items, string item)
    {
        if (items == null)
        {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].Item, item, StringComparison.Ordinal))
            {
                total += items[i].Amount;
            }
        }

        return total;
    }

    static bool TryParseOne(string part, out Ingredient ingredient, out string error)
    {
        ingredient = default;
        error = "";
        var piece = part.Trim();
        var colon = piece.IndexOf(':');
        if (colon <= 0 || colon == piece.Length - 1)
        {
            error = "Each ingredient must look like Item:Amount.";
            return false;
        }

        var item = piece.Substring(0, colon).Trim();
        var amountText = piece.Substring(colon + 1).Trim();
        if (item.Length == 0 || !int.TryParse(amountText, out var amount) || amount <= 0)
        {
            error = "Each ingredient needs a name and a positive amount.";
            return false;
        }

        ingredient = new Ingredient(item, amount);
        return true;
    }
}
