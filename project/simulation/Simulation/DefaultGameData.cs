using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Hard-coded game data that has no editor authoring yet (fighters). Stages come from stage scenes made in the Godot editor.
/// </summary>
public static class DefaultGameData
{
    /// <summary>
    /// The default fighter: walk, jump (with jump squat), double jump, fall, land, and drop through platforms.
    /// </summary>
    public static FighterDefinitionData CreateFighterDefinition()
    {
        var builder = new FighterDefinitionBuilder(CreateFighterStats()) { IdleState = "Idle" };

        const StateFlags FreeOnGround = StateFlags.Actionable | StateFlags.CanTurn;

        builder.State("Idle", s => s
            .Movement(MovementMode.GroundControl)
            .Flags(FreeOnGround)
            .OnLeaveGround("Fall")
            .Transition("Walk", ConditionData.DirectionHeld(DirectionInput.Horizontal)));

        builder.State("Walk", s => s
            .Movement(MovementMode.GroundControl)
            .Flags(FreeOnGround)
            .OnLeaveGround("Fall")
            .Transition("Idle", ConditionData.DirectionHeld(DirectionInput.NoHorizontal)));

        // Short crouch before a ground jump. The fighter keeps its momentum.
        builder.State("JumpSquat", s => s
            .Duration(3, "Jump")
            .Movement(MovementMode.None)
            .OnLeaveGround("Fall"));

        builder.State("Jump", s => s
            .Movement(MovementMode.AirControl)
            .Flags(StateFlags.Actionable)
            .FrameAction(0, FrameActionType.Jump)
            .OnLanding("Land")
            .Transition("Fall", ConditionData.VelocityYDown));

        builder.State("DoubleJump", s => s
            .Movement(MovementMode.AirControl)
            .Flags(StateFlags.Actionable)
            .FrameAction(0, FrameActionType.Jump)
            .OnLanding("Land")
            .Transition("Fall", ConditionData.VelocityYDown));

        builder.State("Fall", s => s
            .Movement(MovementMode.AirControl)
            .Flags(StateFlags.Actionable)
            .OnLanding("Land"));

        // Short recovery after landing. The fighter cannot act.
        builder.State("Land", s => s
            .Duration(3, "Idle")
            .Movement(MovementMode.Locked)
            .OnLeaveGround("Fall"));

        // Down + Jump on a platform: fall through it. This is not a jump.
        builder.State("PlatformDrop", s => s
            .Movement(MovementMode.None)
            .FrameAction(0, FrameActionType.StartDropThrough)
            .OnLeaveGround("Fall"));

        builder.SharedGroundTransition("PlatformDrop",
            ConditionData.Pressed(InputFlags.Jump),
            ConditionData.DirectionHeld(DirectionInput.Down),
            ConditionData.OnPlatform);
        builder.SharedGroundTransition("JumpSquat", ConditionData.Pressed(InputFlags.Jump));

        builder.SharedAirTransition("DoubleJump", ConditionData.Pressed(InputFlags.Jump), ConditionData.HasJumpsLeft);

        return builder.Build();
    }

    public static FighterStats CreateFighterStats() => new()
    {
        CollisionBoxSize = new FixedVector2(48, 96),
        WalkSpeed = 5,
        AirSpeed = Fixed.FromRatio(9, 2),
        AirAcceleration = Fixed.FromRatio(1, 2),
        AirFriction = Fixed.FromRatio(1, 4),
        Gravity = Fixed.FromRatio(6, 10),
        MaxFallSpeed = 12,
        JumpVelocity = -13,
        MaxJumps = 2,
        DropThroughFrames = 12,
    };
}
