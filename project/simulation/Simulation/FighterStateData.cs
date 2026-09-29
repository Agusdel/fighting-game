using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>How the velocity of a fighter is controlled in a state. Gravity applies in all modes.</summary>
public enum MovementMode : byte
{
    /// <summary>No control: the velocity does not change (only gravity).</summary>
    None,

    /// <summary>Walk input sets the horizontal speed. With <see cref="StateFlags.CanTurn"/>, the input also sets the facing.</summary>
    GroundControl,

    /// <summary>Horizontal air acceleration while a direction is held, air friction otherwise.</summary>
    AirControl,

    /// <summary><see cref="GroundControl"/> when grounded, <see cref="AirControl"/> when airborne.</summary>
    Free,

    /// <summary>No control. On the ground the horizontal speed is 0. In the air the fighter keeps its momentum.</summary>
    Locked,

    /// <summary>No control. Friction slows the horizontal speed (used while the fighter is hit).</summary>
    Knockback,
}

[Flags]
public enum StateFlags : ushort
{
    None = 0,

    /// <summary>
    /// The fighter is free to start a new action. The shared transitions of the fighter definition apply
    /// (ground list or air list), before the state's own transitions.
    /// </summary>
    Actionable = 1 << 0,

    /// <summary>Movement input can change the facing (with <see cref="MovementMode.GroundControl"/> or <see cref="MovementMode.Free"/> on the ground).</summary>
    CanTurn = 1 << 1,

    /// <summary>A hit does damage, but does not force the change to hitstun.</summary>
    ArmoredAgainstHits = 1 << 2,
}

public enum ConditionType : byte
{
    /// <summary>All buttons in <see cref="ConditionData.Buttons"/> are pressed on this frame (held now, not held on the last frame).</summary>
    InputPressed,

    /// <summary>All buttons in <see cref="ConditionData.Buttons"/> are held.</summary>
    InputHeld,

    /// <summary>All buttons in <see cref="ConditionData.Buttons"/> are released on this frame (held on the last frame, not held now).</summary>
    InputReleased,

    /// <summary>The direction input matches <see cref="ConditionData.Direction"/>.</summary>
    Direction,

    Grounded,
    Airborne,

    /// <summary>The fighter stands on a one-way platform.</summary>
    OnPlatform,

    /// <summary>The vertical velocity is greater than 0 (the fighter moves down; Y points down).</summary>
    VelocityYDown,

    /// <summary>The fighter has at least one jump left.</summary>
    HasJumpsLeft,

    /// <summary>Calls <see cref="ConditionData.Custom"/>.</summary>
    Custom,
}

/// <summary>A direction input. Forward and back are relative to the fighter facing.</summary>
public enum DirectionInput : byte
{
    /// <summary>Up held, Down not held.</summary>
    Up,

    /// <summary>Down held, Up not held.</summary>
    Down,

    /// <summary>The horizontal direction is the facing direction.</summary>
    Forward,

    /// <summary>The horizontal direction is opposite to the facing direction.</summary>
    Back,

    /// <summary>Left or right is held (not both).</summary>
    Horizontal,

    /// <summary>No horizontal direction (none, or left and right together).</summary>
    NoHorizontal,
}

/// <summary>One condition of a transition.</summary>
public readonly struct ConditionData
{
    public ConditionType Type { get; init; }
    public InputFlags Buttons { get; init; }
    public DirectionInput Direction { get; init; }
    public StateCondition? Custom { get; init; }

    public static ConditionData Pressed(InputFlags buttons) => new() { Type = ConditionType.InputPressed, Buttons = buttons };
    public static ConditionData Held(InputFlags buttons) => new() { Type = ConditionType.InputHeld, Buttons = buttons };
    public static ConditionData Released(InputFlags buttons) => new() { Type = ConditionType.InputReleased, Buttons = buttons };
    public static ConditionData DirectionHeld(DirectionInput direction) => new() { Type = ConditionType.Direction, Direction = direction };
    public static ConditionData Grounded => new() { Type = ConditionType.Grounded };
    public static ConditionData Airborne => new() { Type = ConditionType.Airborne };
    public static ConditionData OnPlatform => new() { Type = ConditionType.OnPlatform };
    public static ConditionData VelocityYDown => new() { Type = ConditionType.VelocityYDown };
    public static ConditionData HasJumpsLeft => new() { Type = ConditionType.HasJumpsLeft };
    public static ConditionData When(StateCondition condition) => new() { Type = ConditionType.Custom, Custom = condition };
}

/// <summary>A rule to change to another state. It applies when all conditions are true and the state frame is inside the window.</summary>
public sealed class TransitionData
{
    public required ushort TargetState { get; init; }
    public required ConditionData[] Conditions { get; init; }

    /// <summary>First state frame where the rule applies (inclusive).</summary>
    public int FromFrame { get; init; }

    /// <summary>Last state frame where the rule applies (inclusive).</summary>
    public int ToFrame { get; init; } = int.MaxValue;
}

public enum FrameActionType : byte
{
    /// <summary>Uses one jump: the vertical velocity becomes the jump velocity of the fighter stats.</summary>
    Jump,

    /// <summary>The fighter ignores one-way platforms for the drop-through time of the fighter stats.</summary>
    StartDropThrough,

    /// <summary>Sets the velocity to <see cref="FrameActionData.Value"/> (X relative to the facing).</summary>
    SetVelocity,

    /// <summary>Sets the horizontal velocity to <see cref="FrameActionData.Value"/>.X (relative to the facing).</summary>
    SetVelocityX,

    /// <summary>Sets the vertical velocity to <see cref="FrameActionData.Value"/>.Y.</summary>
    SetVelocityY,
}

/// <summary>An action that runs on one state frame.</summary>
public readonly struct FrameActionData
{
    public int Frame { get; init; }
    public FrameActionType Type { get; init; }
    public FixedVector2 Value { get; init; }
}

/// <summary>
/// One state of the fighter state machine. Static data: shared by all fighters, never changed during a match.
/// The current state of a fighter is only <see cref="FighterData.StateId"/> and <see cref="FighterData.StateFrame"/>.
/// </summary>
public sealed class FighterStateData
{
    /// <summary>The value for "no state" in <see cref="OnLanding"/> and <see cref="OnLeaveGround"/>.</summary>
    public const ushort NoState = ushort.MaxValue;

    public required ushort Id { get; init; }

    /// <summary>Unique name. The presentation uses it to find the animation, the VFX, and the sounds of the state.</summary>
    public required string Name { get; init; }

    /// <summary>Number of frames. 0 = no end.</summary>
    public int Duration { get; init; }

    /// <summary>The state after <see cref="Duration"/> ends.</summary>
    public ushort NextState { get; init; } = NoState;

    public MovementMode Movement { get; init; }
    public StateFlags Flags { get; init; }

    /// <summary>The state to change to when the fighter lands. <see cref="NoState"/> = stay in this state.</summary>
    public ushort OnLanding { get; init; } = NoState;

    /// <summary>The state to change to when the fighter leaves the ground without a jump (walk off, drop-through). <see cref="NoState"/> = stay.</summary>
    public ushort OnLeaveGround { get; init; } = NoState;

    public FrameActionData[] FrameActions { get; init; } = Array.Empty<FrameActionData>();

    /// <summary>Checked in order. The first transition that matches wins.</summary>
    public TransitionData[] Transitions { get; init; } = Array.Empty<TransitionData>();

    public StateHook? OnEnter { get; init; }
    public StateHook? OnUpdate { get; init; }
    public StateHook? OnExit { get; init; }

    public bool Has(StateFlags flag) => (Flags & flag) != 0;
}
