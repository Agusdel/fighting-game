using System;
using FightingGame.Core;
using FightingGame.Session;
using FightingGame.Simulation;
using FightingGame.Simulation.Tests.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class SyncTestSessionTests
{
    [Fact]
    public void StartsWithTheSameWorldAsWorldDataCreate()
    {
        GameData data = TestStages.CreateGameData();
        var session = new SyncTestSession(data, 2, 7);
        Assert.Equal(WorldData.Create(data, 2, 7).ComputeHash(), session.World.ComputeHash());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public void GivesTheSameFramesAsALocalSession(int checkDistance)
    {
        GameData data = TestStages.CreateGameData();
        var syncTest = new SyncTestSession(data, 2, 7, checkDistance);
        var local = new LocalSession(data, 2, 7);
        InputFlags[] p0 = WorldDataTests.RandomInputs(seed: 1, count: 600);
        InputFlags[] p1 = WorldDataTests.RandomInputs(seed: 2, count: 600);

        for (int i = 0; i < p0.Length; i++)
        {
            syncTest.SetLocalInput(0, p0[i]);
            syncTest.SetLocalInput(1, p1[i]);
            syncTest.AdvanceFrame();
            local.SetLocalInput(0, p0[i]);
            local.SetLocalInput(1, p1[i]);
            local.AdvanceFrame();

            Assert.Equal(local.World.ComputeHash(), syncTest.World.ComputeHash());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LongMatchWithManyRoundsHasNoDesync(int checkDistance)
    {
        // Low health and a short restart delay, so the random inputs end many rounds.
        var data = new GameData
        {
            Stage = TestStages.CreateDefault(),
            FighterDefinition = DefaultGameData.CreateFighterDefinition(DefaultGameData.CreateFighterStats() with { MaxHealth = 10 }),
            Rules = new MatchRulesData { RestartDelayFrames = 30 },
        };
        const int Players = 4;
        var rng = new FixedRng(99);

        var session = new SyncTestSession(data, Players, 11, checkDistance);
        for (int frame = 0; frame < 3000; frame++)
        {
            for (int slot = 0; slot < Players; slot++)
            {
                session.SetLocalInput(slot, TestBot.Input(session.World, slot, ref rng));
            }
            session.AdvanceFrame();
        }

        Assert.True(session.World.Round > 5, $"The bot inputs must end many rounds, or the test proves nothing (round {session.World.Round}).");
    }

    // A hook that breaks the hook rules: it keeps a value in a static field, outside the world state.
    // When a frame runs again, the hook gives a different value.
    private static int s_hiddenCounter;

    private static void CountInStaticField(ref FighterData fighter, in StateContext context) => fighter.StateVar1 = s_hiddenCounter++;

    [Fact]
    public void ReportsStateOutsideTheWorldData()
    {
        var builder = new FighterDefinitionBuilder(DefaultGameData.CreateFighterStats()) { IdleState = "Idle" };
        builder.State("Idle", s => s.Movement(MovementMode.None).OnUpdate(CountInStaticField));
        var data = new GameData { Stage = TestStages.CreateDefault(), FighterDefinition = builder.Build() };
        s_hiddenCounter = 0;
        var session = new SyncTestSession(data, 1, 1);

        SyncTestException error = Assert.Throws<SyncTestException>(() => session.AdvanceFrame());

        // First run of frame 0 -> 1: StateVar1 = 0. Second run from the snapshot of frame 0: StateVar1 = 1.
        Assert.Equal(1, error.Frame);
        Assert.NotEqual(error.ExpectedHash, error.ActualHash);
        FieldDifference difference = Assert.Single(error.Differences);
        Assert.Equal("Fighters[0].StateVar1", difference.Path);
        Assert.Equal("0", difference.Expected);
        Assert.Equal("1", difference.Actual);
        Assert.Contains("Fighters[0].StateVar1: 0 -> 1", error.Message);
    }

    [Fact]
    public void InputsAreClearedAfterEachFrame()
    {
        var session = new SyncTestSession(TestStages.CreateGameData(), 1, 1);
        session.SetLocalInput(0, InputFlags.Right);
        session.AdvanceFrame();
        session.AdvanceFrame();
        Assert.Equal(InputFlags.None, session.World.Fighters[0].PrevInput);
    }

    [Fact]
    public void RejectsInvalidValues()
    {
        GameData data = TestStages.CreateGameData();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SyncTestSession(data, 2, 1, checkDistance: 0));
        var session = new SyncTestSession(data, 2, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(-1, InputFlags.Jump));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(2, InputFlags.Jump));
    }
}
