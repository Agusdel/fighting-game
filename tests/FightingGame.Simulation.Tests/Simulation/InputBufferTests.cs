using System;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

/// <summary>Tests of the input buffer: <see cref="InputBufferData"/> and the <see cref="ConditionType.InputPressed"/> condition.</summary>
public class InputBufferTests
{
    private static MatchRulesData Buffer(int frames) => new() { InputBufferFrames = frames };

    /// <summary>A world with one fighter. "Idle" has no movement and the configured transitions.</summary>
    private static TestWorld WorldWith(MatchRulesData rules, Action<FighterDefinitionBuilder> configure,
        Action<FighterDefinitionBuilder.StateBuilder> idle)
    {
        var builder = new FighterDefinitionBuilder(DefaultGameData.CreateFighterStats()) { IdleState = "Idle" };
        builder.State("Idle", s =>
        {
            s.Movement(MovementMode.None);
            idle(s);
        });
        configure(builder);
        return new TestWorld(definition: builder.Build(), rules: rules);
    }

    private static void Empty(FighterDefinitionBuilder.StateBuilder s)
    {
    }

    /// <summary>Default fighter, landed from a short fall: the state is "Land" (frame 0).</summary>
    private static TestWorld LandedWorld(MatchRulesData rules)
    {
        var world = new TestWorld(rules: rules);
        world.PlaceInAir(new FixedVector2(400, 590), FixedVector2.Zero);
        world.RunUntilGrounded();
        Assert.Equal("Land", world.StateName());
        return world;
    }

    [Fact]
    public void JumpPressedDuringLandStartsAJumpAfterTheLanding()
    {
        TestWorld world = LandedWorld(new MatchRulesData());

        world.Tick(InputFlags.Jump);     // A tap during the landing (Land is not actionable).
        world.Run(world.StateDuration("Land"));

        Assert.Equal("JumpSquat", world.StateName());
    }

    [Fact]
    public void WithoutBufferJumpPressedDuringLandIsLost()
    {
        TestWorld world = LandedWorld(Buffer(0));

        world.Tick(InputFlags.Jump);
        world.Run(world.StateDuration("Land") + 5);

        Assert.Equal("Idle", world.StateName());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public void PressIsValidForTheBufferFramesAfterItsFrame(int bufferFrames)
    {
        // The press is on state frame 1, so it is valid up to state frame 1 + bufferFrames.
        int lastValidFrame = 1 + bufferFrames;

        TestWorld inTime = WorldWith(Buffer(bufferFrames), b => b.State("A", Empty),
            s => s.TransitionInWindow("A", lastValidFrame, 100, ConditionData.Pressed(InputFlags.Attack1)));
        inTime.Tick(InputFlags.Attack1);
        inTime.Run(lastValidFrame - 1);
        Assert.Equal("A", inTime.StateName());

        TestWorld tooLate = WorldWith(Buffer(bufferFrames), b => b.State("A", Empty),
            s => s.TransitionInWindow("A", lastValidFrame + 1, 100, ConditionData.Pressed(InputFlags.Attack1)));
        tooLate.Tick(InputFlags.Attack1);
        tooLate.Run(20);
        Assert.Equal("Idle", tooLate.StateName());
    }

    [Fact]
    public void HeldButtonIsNotPressedAgain()
    {
        TestWorld world = WorldWith(Buffer(6), b => b.State("A", Empty),
            s => s.TransitionInWindow("A", 10, 100, ConditionData.Pressed(InputFlags.Attack1)));

        world.Run(20, InputFlags.Attack1);   // Pressed on frame 1, then only held.

        Assert.Equal("Idle", world.StateName());
    }

    [Fact]
    public void TransitionConsumesItsPressedButtons()
    {
        TestWorld world = WorldWith(Buffer(6),
            b => b.State("A", s => s.Transition("B", ConditionData.Pressed(InputFlags.Attack1))).State("B", Empty),
            s => s.Transition("A", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1);
        world.Run(10);

        Assert.Equal("A", world.StateName());
    }

    [Fact]
    public void TransitionDoesNotConsumeOtherButtons()
    {
        TestWorld world = WorldWith(Buffer(6),
            b => b.State("A", s => s.Transition("B", ConditionData.Pressed(InputFlags.Jump))).State("B", Empty),
            s => s.Transition("A", ConditionData.Pressed(InputFlags.Attack1)));

        world.Tick(InputFlags.Attack1 | InputFlags.Jump);
        Assert.Equal("A", world.StateName());
        world.Tick();
        Assert.Equal("B", world.StateName());
    }

    [Fact]
    public void OneJumpPressGivesOneJump()
    {
        var world = new TestWorld();

        world.Tick(InputFlags.Jump);
        world.Run(10);

        // Jump is actionable and has the air jump transition, but the press was used by the ground jump.
        Assert.Equal("Jump", world.StateName());
        Assert.Equal(world.Stats.MaxJumps - 1, world.Fighter().JumpsLeft);
    }

    [Fact]
    public void PressDuringHitstopStaysValidAfterTheHitstop()
    {
        var world = new TestWorld(playerCount: 2, rules: Buffer(6));
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 450, -1);
        world.Tick(InputFlags.Attack1);
        while (world.Fighter(0).HitstopFrames == 0)
        {
            world.Tick();
        }
        int hitstop = world.Fighter(0).HitstopFrames;

        world.Tick(InputFlags.Jump);   // A tap on the first frozen frame.
        world.Run(hitstop - 1);
        Assert.Equal(0, world.Fighter(0).HitstopFrames);

        // The attack is not actionable, so nothing uses the press. It is valid for the 6 buffer frames + its own frame.
        world.Run(6);
        Assert.True(world.Fighter(0).InputBuffer.Contains(InputFlags.Jump));
        world.Tick();
        Assert.False(world.Fighter(0).InputBuffer.Contains(InputFlags.Jump));
    }

    [Fact]
    public void NewRoundClearsTheBuffer()
    {
        var world = new TestWorld(rules: Buffer(6));
        world.Fighter().InputBuffer.Record(InputFlags.Jump, 6);

        WorldData.StartRound(ref world.State, world.Data);

        Assert.False(world.Fighter().InputBuffer.Contains(InputFlags.Jump));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(MatchRulesData.MaxInputBufferFrames + 1)]
    public void InvalidBufferFramesThrow(int frames)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRulesData { InputBufferFrames = frames });
    }

    [Fact]
    public void ButtonCountMatchesTheInputFlags()
    {
        InputFlags all = InputFlags.None;
        foreach (InputFlags flag in Enum.GetValues<InputFlags>())
        {
            all |= flag;
        }
        Assert.Equal(InputFlagsExtensions.AllButtons, all);
    }
}
