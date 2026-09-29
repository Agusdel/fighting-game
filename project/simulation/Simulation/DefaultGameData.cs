using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Hard-coded game data that has no editor authoring yet (fighters). Stages come from stage scenes made in the Godot editor.
/// </summary>
public static class DefaultGameData
{
    /// <summary>
    /// The default fighter: walk, jump (with jump squat), double jump, fall, land, drop through platforms,
    /// a light attack and a heavy attack (each with forward, up, and down variants), and hitstun.
    /// </summary>
    public static FighterDefinitionData CreateFighterDefinition()
    {
        var builder = new FighterDefinitionBuilder(CreateFighterStats()) { IdleState = "Idle", HitstunState = "Hitstun" };

        // Boxes are relative to the feet, for a fighter that faces right. The fighter body is 48 x 96.
        static FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
            new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

        builder.DefaultHurtbox(Box(-24, -96, 24, 0));

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

        // Light attack: fast and weak. The fighter can move during it (walk on the ground, air control in the air),
        // but it keeps its facing. It works on the ground and in the air, and a landing does not end it.
        const int LightDuration = 18;
        builder.State("Attack1Forward", s => s
            .Duration(LightDuration, "Idle", "Fall")
            .Movement(MovementMode.Free)
            .Hitbox(Box(20, -70, 64, -40), 4, 6, damage: 5, hitstunFrames: 12, knockback: new FixedVector2(4, -2), hitstopFrames: 3));
        builder.State("Attack1Up", s => s
            .Duration(LightDuration, "Idle", "Fall")
            .Movement(MovementMode.Free)
            .Hitbox(Box(-24, -130, 24, -90), 4, 6, damage: 5, hitstunFrames: 12, knockback: new FixedVector2(0, -6), hitstopFrames: 3));
        builder.State("Attack1Down", s => s
            .Duration(LightDuration, "Idle", "Fall")
            .Movement(MovementMode.Free)
            .Hitbox(Box(-20, -10, 20, 30), 4, 6, damage: 5, hitstunFrames: 12, knockback: new FixedVector2(0, 5), hitstopFrames: 3));

        // Heavy attack: slow and strong. No movement control: on the ground the fighter stops,
        // in the air it keeps its momentum.
        const int HeavyDuration = 36;
        builder.State("Attack2Forward", s => s
            .Duration(HeavyDuration, "Idle", "Fall")
            .Movement(MovementMode.Locked)
            .Hitbox(Box(20, -80, 84, -30), 12, 16, damage: 15, hitstunFrames: 24, knockback: new FixedVector2(9, -5), hitstopFrames: 6));
        builder.State("Attack2Up", s => s
            .Duration(HeavyDuration, "Idle", "Fall")
            .Movement(MovementMode.Locked)
            .Hitbox(Box(-30, -150, 30, -90), 12, 16, damage: 15, hitstunFrames: 24, knockback: new FixedVector2(0, -11), hitstopFrames: 6));
        builder.State("Attack2Down", s => s
            .Duration(HeavyDuration, "Idle", "Fall")
            .Movement(MovementMode.Locked)
            .Hitbox(Box(-30, -10, 30, 40), 12, 16, damage: 15, hitstunFrames: 24, knockback: new FixedVector2(0, 8), hitstopFrames: 6));

        // After a hit: no control, the knockback velocity slows down by friction. The length comes from the hit.
        builder.State("Hitstun", s => s
            .Movement(MovementMode.Knockback)
            .Transition("Idle", ConditionData.HitstunEnded, ConditionData.Grounded)
            .Transition("Fall", ConditionData.HitstunEnded));

        builder.SharedGroundTransition("PlatformDrop",
            ConditionData.Pressed(InputFlags.Jump),
            ConditionData.DirectionHeld(DirectionInput.Down),
            ConditionData.OnPlatform);
        builder.SharedGroundTransition("JumpSquat", ConditionData.Pressed(InputFlags.Jump));
        AddAttackTransitions(builder, InputFlags.Attack1, "Attack1", grounded: true);
        AddAttackTransitions(builder, InputFlags.Attack2, "Attack2", grounded: true);

        builder.SharedAirTransition("DoubleJump", ConditionData.Pressed(InputFlags.Jump), ConditionData.HasJumpsLeft);
        AddAttackTransitions(builder, InputFlags.Attack1, "Attack1", grounded: false);
        AddAttackTransitions(builder, InputFlags.Attack2, "Attack2", grounded: false);

        return builder.Build();
    }

    /// <summary>
    /// The direction when the attack starts selects the variant: Up → Up. Down → Down, but on the ground only on a
    /// platform (there is no low attack on solid ground). Otherwise (also Up and Down together) → Forward.
    /// </summary>
    private static void AddAttackTransitions(FighterDefinitionBuilder builder, InputFlags button, string attack, bool grounded)
    {
        var pressed = ConditionData.Pressed(button);
        var up = ConditionData.DirectionHeld(DirectionInput.Up);
        var down = ConditionData.DirectionHeld(DirectionInput.Down);

        if (grounded)
        {
            builder.SharedGroundTransition(attack + "Up", pressed, up);
            builder.SharedGroundTransition(attack + "Down", pressed, down, ConditionData.OnPlatform);
            builder.SharedGroundTransition(attack + "Forward", pressed);
        }
        else
        {
            builder.SharedAirTransition(attack + "Up", pressed, up);
            builder.SharedAirTransition(attack + "Down", pressed, down);
            builder.SharedAirTransition(attack + "Forward", pressed);
        }
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
        MaxHealth = 100,
        GroundFriction = 1,
    };
}
