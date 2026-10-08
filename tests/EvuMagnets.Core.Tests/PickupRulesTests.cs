using System;
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
    public void Handoff_PullsAGroundDropAtTwoMeters()
    {
        var groundGap = (float)Math.Sqrt((2d * 2d) + (1d * 1d));
        Assert.True(PickupRules.NeedsPull(groundGap, PickupRules.VanillaRange, 8f, PickupRules.HandoffMargin));
        Assert.False(PickupRules.NeedsPull(1.4f, PickupRules.VanillaRange, 8f, PickupRules.HandoffMargin));
        Assert.False(PickupRules.NeedsPull(8.01f, PickupRules.VanillaRange, 8f, PickupRules.HandoffMargin));
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
    public void EquippedMagnet_ExchangesWithTheSlotOccupant()
    {
        Assert.True(PickupRules.TryExchangeMagnetSlot(
            true,
            4,
            5,
            0,
            5,
            out var magnetX,
            out var magnetY,
            out var occupantX,
            out var occupantY));
        Assert.Equal(0, magnetX);
        Assert.Equal(5, magnetY);
        Assert.Equal(4, occupantX);
        Assert.Equal(5, occupantY);
        Assert.False(PickupRules.TryExchangeMagnetSlot(true, 0, 5, 0, 5, out _, out _, out _, out _));
        Assert.False(PickupRules.TryExchangeMagnetSlot(false, 1, 2, 0, 5, out _, out _, out _, out _));
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

    [Fact]
    public void Claim_GoesToTheNearestPlayer()
    {
        var others = new[]
        {
            new PickupRules.ClaimDistance(8f, 2),
            new PickupRules.ClaimDistance(12f, 3),
        };

        Assert.True(PickupRules.IsBestClaim(4f, 9, others, PickupRules.ClaimMargin));
        Assert.False(PickupRules.IsBestClaim(10f, 1, others, PickupRules.ClaimMargin));
        Assert.True(PickupRules.IsBestClaim(4f, 9, new PickupRules.ClaimDistance[0], PickupRules.ClaimMargin));
    }

    [Fact]
    public void Claim_BreaksANearTieByPlayerId()
    {
        var nearer = new[] { new PickupRules.ClaimDistance(5.4f, 20) };
        Assert.True(PickupRules.IsBestClaim(5f, 10, nearer, PickupRules.ClaimMargin));
        Assert.False(PickupRules.IsBestClaim(5f, 30, nearer, PickupRules.ClaimMargin));
    }

    [Fact]
    public void Reach_IsAtLeastVanilla()
    {
        Assert.Equal(PickupRules.VanillaRange, PickupRules.Reach(0f));
        Assert.Equal(PickupRules.VanillaRange, PickupRules.Reach(1f));
        Assert.Equal(8f, PickupRules.Reach(8f));
    }

    [Fact]
    public void Claim_IgnoresAPlayerWhoCannotReachTheDrop()
    {
        // A vanilla-range player 3 meters from the drop cannot take it, so they never join the claim list.
        Assert.False(PickupRules.CanReach(3f, PickupRules.Reach(0f)));
        Assert.True(PickupRules.CanReach(3f, PickupRules.Reach(4f)));
        Assert.True(PickupRules.CanReach(1.5f, PickupRules.Reach(0f)));

        var others = new PickupRules.ClaimDistance[0];
        Assert.True(PickupRules.IsBestClaim(8f, 9, others, PickupRules.ClaimMargin));

        var magnetPlayerNearer = new[] { new PickupRules.ClaimDistance(3f, 2) };
        Assert.False(PickupRules.IsBestClaim(8f, 9, magnetPlayerNearer, PickupRules.ClaimMargin));
    }

    [Fact]
    public void Pull_StopsOnThePlayerWithoutChangingHeight()
    {
        PickupRules.PullOffset(0f, 0f, 10f, 0f, PickupRules.PullSpeed, 10f, out var offsetX, out var offsetZ);
        Assert.Equal(10f, offsetX);
        Assert.Equal(0f, offsetZ);
    }

    [Fact]
    public void Pull_StaysOnTheGroundLine()
    {
        PickupRules.PullOffset(0f, 0f, 3f, 4f, PickupRules.PullSpeed, 0.1f, out var offsetX, out var offsetZ);
        Assert.Equal(0.9, offsetX, 3);
        Assert.Equal(1.2, offsetZ, 3);
    }
}
