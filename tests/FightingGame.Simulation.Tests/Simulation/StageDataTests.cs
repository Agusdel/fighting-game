using System;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class StageDataTests
{
    private static FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
        new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

    private static SpawnPositionPair Pair(int ax, int bx) => new(new FixedVector2(ax, 0), new FixedVector2(bx, 0));

    private static StageData Create(
        FixedAABB[]? solids = null,
        FixedAABB[]? platforms = null,
        FixedVector2[]? singles = null,
        SpawnPositionPair[]? pairs = null) =>
        new(
            solids ?? new[] { Box(0, 600, 1000, 650) },
            platforms ?? new[] { Box(400, 400, 600, 410) },
            singles ?? new[] { new FixedVector2(500, 400) },
            pairs ?? new[] { Pair(100, 900), Pair(200, 800) });

    [Fact]
    public void BoundsContainSolidsAndPlatforms()
    {
        StageData stage = Create(
            solids: new[] { Box(0, 600, 1000, 650), Box(-50, 0, 0, 650) },
            platforms: new[] { Box(900, -20, 1100, -10) });
        Assert.Equal(Box(-50, -20, 1100, 650), stage.Bounds);
    }

    [Fact]
    public void RequiresASolid()
    {
        Assert.Throws<ArgumentException>(() => Create(solids: Array.Empty<FixedAABB>()));
    }

    [Fact]
    public void RequiresASingleSpawnPosition()
    {
        Assert.Throws<ArgumentException>(() => Create(singles: Array.Empty<FixedVector2>()));
    }

    [Fact]
    public void RequiresEnoughSpawnPositionPairsForMaxPlayers()
    {
        Assert.Equal(GameConstants.MaxPlayers / 2, StageData.MinSpawnPositionPairs);
        Assert.Throws<ArgumentException>(() => Create(pairs: new[] { Pair(100, 900) }));
    }

    [Fact]
    public void EqualDataGivesTheSameHash()
    {
        Assert.Equal(Create().ComputeHash(), Create().ComputeHash());
    }

    [Fact]
    public void EveryPartOfTheDataChangesTheHash()
    {
        ulong baseline = Create().ComputeHash();

        Assert.NotEqual(baseline, Create(solids: new[] { Box(0, 601, 1000, 650) }).ComputeHash());
        Assert.NotEqual(baseline, Create(platforms: new[] { Box(400, 400, 601, 410) }).ComputeHash());
        Assert.NotEqual(baseline, Create(singles: new[] { new FixedVector2(501, 400) }).ComputeHash());
        Assert.NotEqual(baseline, Create(singles: new[] { new FixedVector2(500, 400), new FixedVector2(600, 400) }).ComputeHash());
        Assert.NotEqual(baseline, Create(pairs: new[] { Pair(100, 900), Pair(200, 801) }).ComputeHash());
        Assert.NotEqual(baseline, Create(pairs: new[] { Pair(200, 800), Pair(100, 900) }).ComputeHash());
    }

    [Fact]
    public void MovingABoxFromSolidsToPlatformsChangesTheHash()
    {
        FixedAABB floor = Box(0, 600, 1000, 650);
        FixedAABB extra = Box(400, 400, 600, 410);
        StageData asSolid = Create(solids: new[] { floor, extra }, platforms: Array.Empty<FixedAABB>());
        StageData asPlatform = Create(solids: new[] { floor }, platforms: new[] { extra });
        Assert.NotEqual(asSolid.ComputeHash(), asPlatform.ComputeHash());
    }
}
