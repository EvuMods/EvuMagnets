using EvuMagnets.Core;
using Xunit;

namespace EvuMagnets.Core.Tests;

public sealed class RecipeTextTests
{
    [Fact]
    public void Craft_ReadsEachIngredient()
    {
        Assert.True(RecipeText.TryParseList(MagnetCatalog.All[0].Craft, out var items, out var error), error);
        Assert.Equal(new[] { "Iron", "Thunderstone", "Ectoplasm" }, Names(items));
        Assert.Equal(new[] { 20, 1, 5 }, Amounts(items));
    }

    [Fact]
    public void Upgrades_AreTenTwentyFortyAndOneTwoFour()
    {
        var tier = MagnetCatalog.All[2];
        Assert.True(RecipeText.TryParseList(tier.Craft, out var craft, out var error), error);
        Assert.True(RecipeText.TryParseUpgrades(tier.Upgrades, out var steps, out error), error);

        Assert.Equal(10, RecipeText.AmountFor(1, "BlackMetal", craft, steps));
        Assert.Equal(1, RecipeText.AmountFor(1, "Thunderstone", craft, steps));
        Assert.Equal(10, RecipeText.AmountFor(1, "Iron", craft, steps));
        Assert.Equal(10, RecipeText.AmountFor(1, "Crystal", craft, steps));

        Assert.Equal(new[] { 10, 20, 40 }, new[]
        {
            RecipeText.AmountFor(2, "BlackMetal", craft, steps),
            RecipeText.AmountFor(3, "BlackMetal", craft, steps),
            RecipeText.AmountFor(4, "BlackMetal", craft, steps),
        });
        Assert.Equal(new[] { 1, 2, 4 }, new[]
        {
            RecipeText.AmountFor(2, "Thunderstone", craft, steps),
            RecipeText.AmountFor(3, "Thunderstone", craft, steps),
            RecipeText.AmountFor(4, "Thunderstone", craft, steps),
        });
        Assert.Equal(0, RecipeText.AmountFor(2, "Iron", craft, steps));
        Assert.Equal(0, RecipeText.AmountFor(2, "Crystal", craft, steps));
        Assert.Equal(0, RecipeText.AmountFor(5, "BlackMetal", craft, steps));
    }

    [Fact]
    public void IronCraft_StacksTheSharedIronAndTheTierIron()
    {
        Assert.True(RecipeText.TryParseList("Iron:20,Thunderstone:1,Ectoplasm:5", out var craft, out _));
        Assert.True(RecipeText.TryParseUpgrades(MagnetCatalog.All[0].Upgrades, out var steps, out _));
        Assert.Equal(20, RecipeText.AmountFor(1, "Iron", craft, steps));
        Assert.Equal(10, RecipeText.AmountFor(2, "Iron", craft, steps));
    }

    [Fact]
    public void Requirements_IncludeUpgradeOnlyItemsWithZeroAtQualityOne()
    {
        Assert.True(RecipeText.TryParseList("Iron:20,Thunderstone:1", out var craft, out _));
        Assert.True(RecipeText.TryParseUpgrades("Silver:10;Silver:20,Thunderstone:2;Silver:40", out var steps, out _));

        var all = RecipeText.Requirements(craft, steps);
        Assert.Equal(new[] { "Iron", "Thunderstone", "Silver" }, Names(all));
        Assert.Equal(new[] { 20, 1, 0 }, Amounts(all));

        Assert.Equal(0, RecipeText.AmountFor(1, "Silver", craft, steps));
        Assert.Equal(10, RecipeText.AmountFor(2, "Silver", craft, steps));
        Assert.Equal(0, RecipeText.AmountFor(2, "Iron", craft, steps));
    }

    [Fact]
    public void Cast_UsesTheBlackForgeRecipe()
    {
        Assert.Equal("blackforge", MagnetCast.Station);
        Assert.Equal(1, MagnetCast.StationLevel);
        Assert.Equal("piece_FrostFoundry", MagnetCast.Foundry);
        Assert.Equal("evu_magnet_bloodgold", MagnetCast.OutputPrefab);
        Assert.True(RecipeText.TryParseList(MagnetCast.Craft, out var items, out var error), error);
        Assert.Equal(new[] { "Iron", "Thunderstone", "Gold", "FrostCore" }, Names(items));
        Assert.Equal(new[] { 10, 1, 10, 5 }, Amounts(items));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Iron")]
    [InlineData("Iron:0")]
    [InlineData(":5")]
    public void List_RejectsABadIngredient(string text)
    {
        Assert.False(RecipeText.TryParseList(text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Upgrades_RequireThreeGroups()
    {
        Assert.False(RecipeText.TryParseUpgrades("Iron:10,Thunderstone:1;Iron:20,Thunderstone:2", out _, out _));
    }

    static string[] Names(System.Collections.Generic.IReadOnlyList<Ingredient> items)
    {
        var names = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            names[i] = items[i].Item;
        }

        return names;
    }

    static int[] Amounts(System.Collections.Generic.IReadOnlyList<Ingredient> items)
    {
        var amounts = new int[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            amounts[i] = items[i].Amount;
        }

        return amounts;
    }
}
