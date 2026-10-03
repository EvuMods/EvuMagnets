using EvuMagnets.Core;
using Xunit;

namespace EvuMagnets.Core.Tests;

public sealed class PickupRulesTests
{
    [Fact]
    public void Ranges_MatchTheTierTable()
    {
        Assert.Equal(new[] { 4f, 5f, 6f, 8f }, MagnetCatalog.All[0].Ranges);
        Assert.Equal(new[] { 8f, 10f, 12f, 14f }, MagnetCatalog.All[1].Ranges);
        Assert.Equal(new[] { 14f, 16f, 18f, 20f }, MagnetCatalog.All[2].Ranges);
        Assert.Equal(new[] { 20f, 22f, 24f, 28f }, MagnetCatalog.All[3].Ranges);
        Assert.Equal(new[] { 36f }, MagnetCatalog.All[4].Ranges);
        Assert.False(MagnetCatalog.All[4].Forged);
    }

    [Fact]
    public void NewTier_StartsWhereThePreviousTierEnds()
    {
        for (var i = 1; i < MagnetCatalog.All.Count; i++)
        {
            if (!MagnetCatalog.All[i].Forged)
            {
                continue;
            }

            var previous = MagnetCatalog.All[i - 1].Ranges;
            Assert.Equal(previous[previous.Length - 1], MagnetCatalog.All[i].Ranges[0]);
        }
    }

    [Fact]
    public void Cast_IsNotAMagnet()
    {
        Assert.False(PickupRules.IsMagnet(MagnetCast.PrefabName));
        Assert.True(PickupRules.IsMagnet(MagnetCast.OutputPrefab));
        Assert.Equal(36f, PickupRules.RangeForQuality(MagnetCatalog.All[4].Ranges, 1));
    }

    [Theory]
    [InlineData(1, 4f)]
    [InlineData(4, 8f)]
    [InlineData(0, 4f)]
    [InlineData(9, 8f)]
    public void Quality_ClampsIntoTheTier(int quality, float expected)
    {
        Assert.Equal(expected, PickupRules.RangeForQuality(MagnetCatalog.All[0].Ranges, quality));
    }

    [Fact]
    public void Carry_StopsAtTheLimit()
    {
        Assert.True(PickupRules.FitsCarry(298f, 2f, 300f));
        Assert.True(PickupRules.FitsCarry(300f, 0f, 300f));
        Assert.False(PickupRules.FitsCarry(299f, 2f, 300f));
    }

    [Fact]
    public void ExtraRing_StartsPastVanilla()
    {
        Assert.False(PickupRules.InExtraRing(2f, PickupRules.VanillaRange, 8f));
        Assert.True(PickupRules.InExtraRing(2.01f, PickupRules.VanillaRange, 8f));
        Assert.True(PickupRules.InExtraRing(8f, PickupRules.VanillaRange, 8f));
        Assert.False(PickupRules.InExtraRing(8.01f, PickupRules.VanillaRange, 8f));
    }

    [Fact]
    public void OwnWard_IsAlwaysAllowed()
    {
        Assert.True(PickupRules.AllowInWard(pullThroughAllWards: false, playerHasAccess: true));
        Assert.False(PickupRules.AllowInWard(pullThroughAllWards: false, playerHasAccess: false));
        Assert.True(PickupRules.AllowInWard(pullThroughAllWards: true, playerHasAccess: false));
    }

    [Fact]
    public void OneMagnet_IsRequired()
    {
        Assert.Null(PickupRules.ActivePrefab(false, null, new string[0]));
        Assert.Equal("evu_magnet_iron", PickupRules.ActivePrefab(false, null, new[] { "evu_magnet_iron" }));
        Assert.Null(PickupRules.ActivePrefab(false, null, new[] { "evu_magnet_iron", "evu_magnet_silver" }));
        Assert.Equal("evu_magnet_iron", PickupRules.ActivePrefab(false, null, new[] { "SwordIron", "evu_magnet_iron" }));
    }

    [Fact]
    public void MagnetSlot_WinsOverTheTrinketSlot()
    {
        Assert.Equal(
            "evu_magnet_silver",
            PickupRules.ActivePrefab(true, "evu_magnet_silver", new[] { "evu_magnet_silver" }));
        Assert.Null(PickupRules.ActivePrefab(true, null, new[] { "evu_magnet_iron" }));
        Assert.Null(PickupRules.ActivePrefab(true, "evu_magnet_silver", new[] { "evu_magnet_iron", "evu_magnet_silver" }));
        Assert.Equal(
            "evu_magnet_flametal",
            PickupRules.ActivePrefab(true, "evu_magnet_flametal", new string[0]));
    }
}
