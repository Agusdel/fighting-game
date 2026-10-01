using System;
using FightingGame.Session;
using FightingGame.Simulation;
using FightingGame.Simulation.Tests.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class LocalSessionTests
{
    [Fact]
    public void StartsWithTheSameWorldAsWorldDataCreate()
    {
        GameData data = TestStages.CreateGameData();
        var session = new LocalSession(data, 2, 7);
        Assert.Equal(WorldData.Create(data, 2, 7).ComputeHash(), session.World.ComputeHash());
    }

    [Fact]
    public void AdvanceFrameRunsTheSimulationWithTheLocalInputs()
    {
        GameData data = TestStages.CreateGameData();
        var session = new LocalSession(data, 2, 7);
        WorldData expected = WorldData.Create(data, 2, 7);
        InputFlags[] p0 = WorldDataTests.RandomInputs(seed: 1, count: 300);
        InputFlags[] p1 = WorldDataTests.RandomInputs(seed: 2, count: 300);

        for (int i = 0; i < p0.Length; i++)
        {
            session.SetLocalInput(0, p0[i]);
            session.SetLocalInput(1, p1[i]);
            session.AdvanceFrame();

            FrameInput input = default;
            input[0] = p0[i];
            input[1] = p1[i];
            Simulator.Tick(ref expected, input, data);

            Assert.Equal(expected.ComputeHash(), session.World.ComputeHash());
        }
    }

    [Fact]
    public void InputsAreClearedAfterEachFrame()
    {
        var session = new LocalSession(TestStages.CreateGameData(), 1, 1);
        session.SetLocalInput(0, InputFlags.Right);
        session.AdvanceFrame();
        session.AdvanceFrame();
        Assert.Equal(InputFlags.None, session.World.Fighters[0].PrevInput);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void RejectsASlotThatIsNotInTheMatch(int slot)
    {
        var session = new LocalSession(TestStages.CreateGameData(), 2, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(slot, InputFlags.Jump));
    }

    [Fact]
    public void WorldShowsTheCurrentState()
    {
        IMatchSession session = new LocalSession(TestStages.CreateGameData(), 1, 1);
        ref readonly WorldData world = ref session.World;
        session.AdvanceFrame();
        Assert.Equal(1, world.Frame);
    }
}
