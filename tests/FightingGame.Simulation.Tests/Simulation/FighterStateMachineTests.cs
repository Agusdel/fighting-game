using System;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

/// <summary>Tests of the state machine rules, with small fighter definitions made for each test.</summary>
public class FighterStateMachineTests
{
    /// <summary>A world with one fighter on the floor. The definition has an "Idle" state (no movement) plus the configured states.</summary>
    private static TestWorld WorldWith(Action<FighterDefinitionBuilder> configure, StateFlags idleFlags = StateFlags.None,
        Action<FighterDefinitionBuilder.StateBuilder>? idle = null)
    {
        var builder = new FighterDefinitionBuilder(DefaultGameData.CreateFighterStats()) { IdleState = "Idle" };
        builder.State("Idle", s =>
        {
            s.Movement(MovementMode.None).Flags(idleFlags);
            idle?.Invoke(s);
        });
        configure(builder);
        return new TestWorld(definition: builder.Build());
    }

    private static void Empty(FighterDefinitionBuilder.StateBuilder s)
    {
    }

    // Hooks for the tests. Hooks must be static methods that change only the fighter they receive.
    private static void SetVar0To100(ref FighterData fighter, in StateContext context) => fighter.StateVar0 = 100;
    private static void IncrementVar1(ref FighterData fighter, in StateContext context) => fighter.StateVar1++;
    private static void MarkExit(ref FighterData fighter, in StateContext context) => fighter.DropThroughTimer = 77;
    private static bool IsFacingLeft(in FighterData fighter, in StateContext context) => fighter.Facing < 0;

    [Fact]
    public void FirstMatchingTransitionWins()
    {
        TestWorld world = WorldWith(
            b => b.State("A", Empty).State("B", Empty),
            idle: s => s
                .Transition("A", ConditionData.Pressed(InputFlags.Attack1))
                .Transition("B", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        Assert.Equal("A", world.StateName());
    }

    [Fact]
    public void AllConditionsMustBeTrue()
    {
        TestWorld world = WorldWith(
            b => b.State("A", Empty),
            idle: s => s.Transition("A", ConditionData.Held(InputFlags.Attack1), ConditionData.DirectionHeld(DirectionInput.Up)));

        world.Tick(InputFlags.Attack1);
        Assert.Equal("Idle", world.StateName());
        world.Tick(InputFlags.Attack1 | InputFlags.Up);
        Assert.Equal("A", world.StateName());
    }

    [Fact]
    public void TransitionAppliesOnlyInsideItsFrameWindow()
    {
        TestWorld world = WorldWith(
            b => b.State("A", Empty),
            idle: s => s.TransitionInWindow("A", 3, 4, ConditionData.Held(InputFlags.Attack1)));

        world.Run(2, InputFlags.Attack1);       // State frames 1 and 2.
        Assert.Equal("Idle", world.StateName());
        world.Tick(InputFlags.Attack1);         // State frame 3.
        Assert.Equal("A", world.StateName());
    }

    [Fact]
    public void DurationEndChangesToTheNextState()
    {
        TestWorld world = WorldWith(
            b => b.State("A", s => s.Duration(5, "B")).State("B", Empty),
            idle: s => s.Transition("A", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        world.Run(4);
        Assert.Equal("A", world.StateName());
        Assert.Equal(4, world.Fighter().StateFrame);
        world.Tick();
        Assert.Equal("B", world.StateName());
        Assert.Equal(0, world.Fighter().StateFrame);
    }

    [Fact]
    public void AfterADurationEndTheNewStateRulesWaitOneFrame()
    {
        TestWorld world = WorldWith(
            b => b
                .State("A", s => s.Duration(2, "B"))
                .State("B", s => s.Transition("C", ConditionData.Held(InputFlags.Attack1)))
                .State("C", Empty),
            idle: s => s.Transition("A", ConditionData.Pressed(InputFlags.Jump)));

        world.Tick(InputFlags.Jump);
        world.Tick();
        world.Tick(InputFlags.Attack1);    // A ends: only the change to B on this frame.
        Assert.Equal("B", world.StateName());
        world.Tick(InputFlags.Attack1);
        Assert.Equal("C", world.StateName());
    }

    [Fact]
    public void SharedTransitionsHavePriorityInActionableStates()
    {
        TestWorld world = WorldWith(
            b => b
                .State("Own", Empty)
                .State("Shared", Empty)
                .SharedGroundTransition("Shared", ConditionData.Pressed(InputFlags.Attack1)),
            idleFlags: StateFlags.Actionable,
            idle: s => s.Transition("Own", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        Assert.Equal("Shared", world.StateName());
    }

    [Fact]
    public void SharedTransitionsDoNotApplyToBusyStates()
    {
        TestWorld world = WorldWith(
            b => b.State("Shared", Empty).SharedGroundTransition("Shared", ConditionData.Pressed(InputFlags.Attack1)),
            idleFlags: StateFlags.None);

        world.Tick(InputFlags.Attack1);
        Assert.Equal("Idle", world.StateName());
    }

    [Fact]
    public void SharedListDependsOnGrounded()
    {
        TestWorld world = WorldWith(
            b => b
                .State("GroundAction", Empty)
                .State("AirAction", Empty)
                .SharedGroundTransition("GroundAction", ConditionData.Pressed(InputFlags.Attack1))
                .SharedAirTransition("AirAction", ConditionData.Pressed(InputFlags.Attack1)),
            idleFlags: StateFlags.Actionable);

        world.Fighter().Position = new FixedVector2(200, 200);
        world.Fighter().Grounded = false;
        world.Tick(InputFlags.Attack1);
        Assert.Equal("AirAction", world.StateName());
    }

    [Fact]
    public void TransitionToTheCurrentStateRestartsIt()
    {
        TestWorld world = WorldWith(
            b => b.State("A", s => s.OnEnter(SetVar0To100).Transition("A", ConditionData.Pressed(InputFlags.Attack1))),
            idle: s => s.Transition("A", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        world.Run(3);
        Assert.Equal(3, world.Fighter().StateFrame);
        world.Fighter().StateVar0 = 5;

        world.Tick(InputFlags.Attack1);
        Assert.Equal("A", world.StateName());
        Assert.Equal(0, world.Fighter().StateFrame);
        Assert.Equal(100, world.Fighter().StateVar0);
    }

    [Fact]
    public void HooksRunAndStateVarsResetOnEnter()
    {
        TestWorld world = WorldWith(
            b => b
                .State("A", s => s.OnEnter(SetVar0To100).OnUpdate(IncrementVar1).OnExit(MarkExit)
                    .Transition("B", ConditionData.Pressed(InputFlags.Jump)))
                .State("B", Empty),
            idle: s => s.Transition("A", ConditionData.Pressed(InputFlags.Attack1)));

        world.Fighter().StateVar2 = 9;
        world.Tick(InputFlags.Attack1);
        Assert.Equal(100, world.Fighter().StateVar0);
        Assert.Equal(1, world.Fighter().StateVar1);   // OnUpdate also runs on the first frame.
        Assert.Equal(0, world.Fighter().StateVar2);   // Reset on enter.

        world.Run(2);
        Assert.Equal(3, world.Fighter().StateVar1);

        world.Tick(InputFlags.Jump);
        Assert.Equal("B", world.StateName());
        Assert.Equal(77, world.Fighter().DropThroughTimer);
        Assert.Equal(0, world.Fighter().StateVar0);
    }

    [Fact]
    public void PressedHeldAndReleased()
    {
        TestWorld world = WorldWith(
            b => b
                .State("Holding", s => s.Transition("Released", ConditionData.Released(InputFlags.Attack1)))
                .State("Released", Empty),
            idle: s => s.Transition("Holding", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        Assert.Equal("Holding", world.StateName());
        world.Run(5, InputFlags.Attack1);
        Assert.Equal("Holding", world.StateName());
        world.Tick();
        Assert.Equal("Released", world.StateName());
    }

    [Theory]
    [InlineData(1, InputFlags.Right, "Forward")]
    [InlineData(1, InputFlags.Left, "Back")]
    [InlineData(-1, InputFlags.Left, "Forward")]
    [InlineData(-1, InputFlags.Right, "Back")]
    [InlineData(1, InputFlags.Up, "Up")]
    [InlineData(1, InputFlags.Down, "Down")]
    [InlineData(1, InputFlags.Up | InputFlags.Down, "Idle")]
    [InlineData(1, InputFlags.Left | InputFlags.Right, "Idle")]
    public void DirectionsAreRelativeToFacing(int facing, InputFlags input, string expected)
    {
        TestWorld world = WorldWith(
            b => b.State("Forward", Empty).State("Back", Empty).State("Up", Empty).State("Down", Empty),
            idle: s => s
                .Transition("Forward", ConditionData.DirectionHeld(DirectionInput.Forward))
                .Transition("Back", ConditionData.DirectionHeld(DirectionInput.Back))
                .Transition("Up", ConditionData.DirectionHeld(DirectionInput.Up))
                .Transition("Down", ConditionData.DirectionHeld(DirectionInput.Down)));

        world.Fighter().Facing = (sbyte)facing;
        world.Tick(input);
        Assert.Equal(expected, world.StateName());
    }

    [Fact]
    public void CustomCondition()
    {
        TestWorld world = WorldWith(
            b => b.State("A", Empty),
            idle: s => s.Transition("A", ConditionData.When(IsFacingLeft)));

        world.Fighter().Facing = 1;
        world.Tick();
        Assert.Equal("Idle", world.StateName());
        world.Fighter().Facing = -1;
        world.Tick();
        Assert.Equal("A", world.StateName());
    }

    [Fact]
    public void FrameActionRunsOnItsFrameRelativeToFacing()
    {
        TestWorld world = WorldWith(
            b => b.State("Dash", s => s.Movement(MovementMode.None).FrameAction(2, FrameActionType.SetVelocityX, new FixedVector2(8, 0))),
            idle: s => s.Transition("Dash", ConditionData.Pressed(InputFlags.Attack1)));

        world.Fighter().Facing = -1;
        world.Tick(InputFlags.Attack1);
        world.Tick();
        Assert.Equal(Fixed.Zero, world.Fighter().Velocity.X);
        world.Tick();
        Assert.Equal((Fixed)(-8), world.Fighter().Velocity.X);
    }

    [Fact]
    public void JumpPressWinsOverStartingToWalk()
    {
        var world = new TestWorld();
        world.Tick(InputFlags.Right | InputFlags.Jump);
        Assert.Equal("JumpSquat", world.StateName());
    }
}
