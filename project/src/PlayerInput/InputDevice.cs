using FightingGame.Simulation;
using Godot;

namespace FightingGame.PlayerInput;

/// <summary>
/// One set of player controls (a keyboard set or one controller), read from actions of the InputMap
/// (Project Settings). The actions of a set share a prefix: <c>{prefix}_left</c>, <c>{prefix}_jump</c>, ...
/// </summary>
/// <remarks>
/// The device is read once per simulation tick (<see cref="Read"/>). The simulation only gets the resulting
/// <see cref="InputFlags"/>, so the input source has no effect on determinism.
/// </remarks>
public sealed class InputDevice
{
    private readonly StringName _left;
    private readonly StringName _right;
    private readonly StringName _up;
    private readonly StringName _down;
    private readonly StringName _jump;
    private readonly StringName _attack1;
    private readonly StringName _attack2;

    public InputDevice(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
        _left = Action(id, "left");
        _right = Action(id, "right");
        _up = Action(id, "up");
        _down = Action(id, "down");
        _jump = Action(id, "jump");
        _attack1 = Action(id, "attack1");
        _attack2 = Action(id, "attack2");
    }

    /// <summary>The names of the actions of one set, without the prefix.</summary>
    public static readonly string[] ActionSuffixes = { "left", "right", "up", "down", "jump", "attack1", "attack2" };

    /// <summary>The action prefix and a unique id, for example "keyboard1" or "controller2".</summary>
    public string Id { get; }

    /// <summary>The name shown to players, for example "Keyboard 1" or "Controller 2".</summary>
    public string DisplayName { get; }

    /// <summary>The buttons of this device that are held now.</summary>
    public InputFlags Read()
    {
        InputFlags flags = InputFlags.None;
        if (Input.IsActionPressed(_left)) flags |= InputFlags.Left;
        if (Input.IsActionPressed(_right)) flags |= InputFlags.Right;
        if (Input.IsActionPressed(_up)) flags |= InputFlags.Up;
        if (Input.IsActionPressed(_down)) flags |= InputFlags.Down;
        if (Input.IsActionPressed(_jump)) flags |= InputFlags.Jump;
        if (Input.IsActionPressed(_attack1)) flags |= InputFlags.Attack1;
        if (Input.IsActionPressed(_attack2)) flags |= InputFlags.Attack2;
        return flags;
    }

    /// <summary>True if the event presses the join button of this device (jump or light attack).</summary>
    public bool IsJoinPress(InputEvent inputEvent) =>
        inputEvent.IsActionPressed(_jump) || inputEvent.IsActionPressed(_attack1);

    /// <summary>True if the event presses the leave button of this device (heavy attack).</summary>
    public bool IsLeavePress(InputEvent inputEvent) => inputEvent.IsActionPressed(_attack2);

    public override string ToString() => DisplayName;

    private static StringName Action(string prefix, string suffix) => new($"{prefix}_{suffix}");
}
