using System.Linq;
using FightingGame.Core;
using FightingGame.Session;
using FightingGame.Simulation;
using FightingGame.Simulation.Tests.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class WorldDataDiffTests
{
    private static WorldData CreateWorld() => WorldData.Create(TestStages.CreateGameData(), 4, 5);

    [Fact]
    public void EqualStatesHaveNoDifferences()
    {
        WorldData world = CreateWorld();
        Assert.Empty(WorldDataDiff.Compare(world, world));
    }

    [Fact]
    public void FindsEachChangedField()
    {
        WorldData expected = CreateWorld();
        WorldData actual = expected;
        actual.Frame = 9;
        actual.Phase = MatchPhase.RoundOver;
        actual.Rng.NextULong();
        actual.Fighters[1].Position = new FixedVector2(Fixed.FromInt(1), Fixed.FromRaw(Fixed.OneRaw / 2));
        actual.Fighters[3].Health = 1;
        actual.Fighters[3].PrevInput = InputFlags.Jump;

        string[] paths = WorldDataDiff.Compare(expected, actual).Select(d => d.Path).ToArray();

        Assert.Equal(new[]
        {
            "Frame",
            "Rng._state",
            "Phase",
            "Fighters[1].Position",
            "Fighters[3].PrevInput",
            "Fighters[3].Health",
        }, paths);
    }

    [Fact]
    public void ShowsReadableValues()
    {
        WorldData expected = CreateWorld();
        WorldData actual = expected;
        actual.Fighters[2].Position = new FixedVector2(Fixed.FromInt(10), Fixed.FromRaw(Fixed.OneRaw / 2));
        actual.Fighters[2].Health = 7;
        actual.Fighters[2].PrevInput = InputFlags.Left | InputFlags.Jump;

        FieldDifference[] differences = WorldDataDiff.Compare(expected, actual).ToArray();

        Assert.Equal($"Fighters[2].Position: {expected.Fighters[2].Position} -> (10, 0.5)", differences[0].ToString());
        Assert.Equal("Fighters[2].PrevInput: None -> Left, Jump", differences[1].ToString());
        Assert.Equal($"Fighters[2].Health: {expected.Fighters[2].Health} -> 7", differences[2].ToString());
    }
}
