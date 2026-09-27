using System;
using System.Collections.Generic;
using System.Linq;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class SpawnPositionSelectorTests
{
    private static readonly FixedVector2[] Singles = { new(501, 100), new(502, 100) };

    /// <summary>
    /// A stage with 2 single spawn positions and 3 pairs. Each pair n has x values 10n+1 and 10n+2,
    /// so the pair of a position is easy to find.
    /// </summary>
    private static StageData CreateStage()
    {
        FixedAABB floor = new(new FixedVector2(0, 600), new FixedVector2(1000, 650));
        SpawnPositionPair[] pairs =
        {
            new(new FixedVector2(11, 0), new FixedVector2(12, 0)),
            new(new FixedVector2(21, 0), new FixedVector2(22, 0)),
            new(new FixedVector2(31, 0), new FixedVector2(32, 0)),
        };
        return new StageData(new[] { floor }, Array.Empty<FixedAABB>(), Singles, pairs);
    }

    private static FixedVector2[] Select(StageData stage, int playerCount, ulong seed)
    {
        var rng = new FixedRng(seed);
        var result = new FixedVector2[GameConstants.MaxPlayers];
        SpawnPositionSelector.Select(stage, playerCount, ref rng, result);
        return result.Take(playerCount).ToArray();
    }

    private static bool IsSingle(FixedVector2 position) => Singles.Contains(position);

    private static int PairOf(FixedVector2 position) => position.X.FloorToInt() / 10;

    [Fact]
    public void OnePlayerUsesASingleSpawnPosition()
    {
        for (ulong seed = 0; seed < 20; seed++)
        {
            FixedVector2[] positions = Select(CreateStage(), 1, seed);
            Assert.True(IsSingle(positions[0]));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UsesFullPairsAndOneSingleOnlyForOddCounts(int playerCount)
    {
        StageData stage = CreateStage();
        for (ulong seed = 0; seed < 50; seed++)
        {
            FixedVector2[] positions = Select(stage, playerCount, seed);

            Assert.Equal(playerCount, positions.Distinct().Count());
            Assert.Equal(playerCount % 2, positions.Count(IsSingle));

            // Every chosen pair is used with both of its positions.
            List<int> pairs = positions.Where(p => !IsSingle(p)).Select(PairOf).ToList();
            Assert.Equal(playerCount / 2, pairs.Distinct().Count());
            Assert.All(pairs.GroupBy(p => p), group => Assert.Equal(2, group.Count()));
        }
    }

    [Fact]
    public void SameSeedGivesTheSameSpawnPositions()
    {
        StageData stage = CreateStage();
        Assert.Equal(Select(stage, 4, 99), Select(stage, 4, 99));
        Assert.Equal(Select(stage, 3, 99), Select(stage, 3, 99));
    }

    [Fact]
    public void AllPairsAllSinglesAndAllPositionsAreUsedOverManySeeds()
    {
        StageData stage = CreateStage();
        var pairsSeen = new HashSet<int>();
        var slot0Positions = new HashSet<FixedVector2>();
        var singlesSeen = new HashSet<FixedVector2>();
        for (ulong seed = 0; seed < 200; seed++)
        {
            FixedVector2[] twoPlayers = Select(stage, 2, seed);
            pairsSeen.Add(PairOf(twoPlayers[0]));
            slot0Positions.Add(twoPlayers[0]);

            singlesSeen.Add(Select(stage, 1, seed)[0]);
        }

        Assert.Equal(3, pairsSeen.Count);
        Assert.Equal(6, slot0Positions.Count);
        Assert.Equal(Singles.Length, singlesSeen.Count);
    }

    [Fact]
    public void AdvancesTheRandomGenerator()
    {
        var rng = new FixedRng(5);
        ulong before = rng.State;
        SpawnPositionSelector.Select(CreateStage(), 2, ref rng, new FixedVector2[GameConstants.MaxPlayers]);
        Assert.NotEqual(before, rng.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GameConstants.MaxPlayers + 1)]
    public void RejectsInvalidPlayerCount(int playerCount)
    {
        var rng = new FixedRng(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpawnPositionSelector.Select(CreateStage(), playerCount, ref rng, new FixedVector2[8]));
    }
}
