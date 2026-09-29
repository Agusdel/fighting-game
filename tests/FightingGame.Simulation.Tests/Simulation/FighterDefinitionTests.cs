using System;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class FighterDefinitionTests
{
    private static void Empty(FighterDefinitionBuilder.StateBuilder s)
    {
    }

    private static void HookA(ref FighterData fighter, in StateContext context)
    {
    }

    private static void HookB(ref FighterData fighter, in StateContext context)
    {
    }

    private static FighterDefinitionBuilder NewBuilder() =>
        new(DefaultGameData.CreateFighterStats()) { IdleState = "Idle" };

    [Fact]
    public void StateIdsFollowTheOrderOfTheStates()
    {
        FighterDefinitionData definition = NewBuilder().State("Idle", Empty).State("A", Empty).Build();
        Assert.Equal(0, definition.FindState("Idle"));
        Assert.Equal(1, definition.FindState("A"));
        Assert.Equal(0, definition.IdleState);
        Assert.Throws<ArgumentException>(() => definition.FindState("Missing"));
    }

    [Fact]
    public void BuildReportsAllErrors()
    {
        FighterDefinitionBuilder builder = NewBuilder()
            .State("Idle", s => s.Transition("Missing1", ConditionData.Grounded))
            .State("Idle", Empty)
            .SharedAirTransition("Missing2", ConditionData.Airborne);
        builder.IdleState = "Missing3";

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("Missing1", exception.Message);
        Assert.Contains("Missing2", exception.Message);
        Assert.Contains("Missing3", exception.Message);
        Assert.Contains("Two states are named 'Idle'", exception.Message);
    }

    [Fact]
    public void DefaultDefinitionHasTheExpectedStates()
    {
        FighterDefinitionData definition = DefaultGameData.CreateFighterDefinition();
        foreach (string name in new[] { "Idle", "Walk", "JumpSquat", "Jump", "DoubleJump", "Fall", "Land", "PlatformDrop" })
        {
            definition.FindState(name);
        }
    }

    [Fact]
    public void SameDefinitionGivesTheSameHash()
    {
        Assert.Equal(
            DefaultGameData.CreateFighterDefinition().ComputeHash(),
            DefaultGameData.CreateFighterDefinition().ComputeHash());
    }

    [Fact]
    public void EveryPartOfTheDefinitionChangesTheHash()
    {
        ulong Hash(Action<FighterDefinitionBuilder.StateBuilder> configureA) =>
            NewBuilder().State("Idle", Empty).State("A", configureA).State("B", Empty).Build().ComputeHash();

        ulong baseline = Hash(Empty);
        Assert.NotEqual(baseline, Hash(s => s.Duration(5, "B")));
        Assert.NotEqual(baseline, Hash(s => s.Movement(MovementMode.Locked)));
        Assert.NotEqual(baseline, Hash(s => s.Flags(StateFlags.CanTurn)));
        Assert.NotEqual(baseline, Hash(s => s.OnLanding("B")));
        Assert.NotEqual(baseline, Hash(s => s.OnLeaveGround("B")));
        Assert.NotEqual(baseline, Hash(s => s.FrameAction(3, FrameActionType.Jump)));
        Assert.NotEqual(baseline, Hash(s => s.Transition("B", ConditionData.Grounded)));
        Assert.NotEqual(baseline, Hash(s => s.TransitionInWindow("B", 1, 2, ConditionData.Grounded)));
        Assert.NotEqual(Hash(s => s.OnEnter(HookA)), Hash(s => s.OnEnter(HookB)));
        Assert.NotEqual(Hash(s => s.OnEnter(HookA)), Hash(s => s.OnUpdate(HookA)));
    }

    [Fact]
    public void StatsChangeTheHash()
    {
        FighterStats stats = DefaultGameData.CreateFighterStats();
        FighterStats faster = stats with { WalkSpeed = stats.WalkSpeed + 1 };

        ulong Hash(FighterStats s) =>
            new FighterDefinitionBuilder(s) { IdleState = "Idle" }.State("Idle", Empty).Build().ComputeHash();

        Assert.NotEqual(Hash(stats), Hash(faster));
    }
}
