using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Maps physical keyboard keys to <see cref="InputFlags"/> for one player.
/// Physical keys use the key position, so the map works on every keyboard layout (QWERTY, AZERTY, ...).
/// </summary>
/// <remarks>
/// The map reads keys directly and does not use Godot input actions. With direct keys, two players can share one keyboard
/// with different key sets.
/// </remarks>
public sealed class KeyboardInputMap
{
    public required Key Left { get; init; }
    public required Key Right { get; init; }
    public required Key Up { get; init; }
    public required Key Down { get; init; }
    public required Key Jump { get; init; }
    public required Key Attack1 { get; init; }
    public required Key Attack2 { get; init; }

    /// <summary>W A S D to move, Space to jump, J for the light attack, K for the heavy attack.</summary>
    public static KeyboardInputMap Wasd { get; } = new()
    {
        Left = Key.A,
        Right = Key.D,
        Up = Key.W,
        Down = Key.S,
        Jump = Key.Space,
        Attack1 = Key.J,
        Attack2 = Key.K,
    };

    /// <summary>Arrow keys to move, keypad 0 to jump, keypad 1 for the light attack, keypad 2 for the heavy attack.</summary>
    public static KeyboardInputMap Arrows { get; } = new()
    {
        Left = Key.Left,
        Right = Key.Right,
        Up = Key.Up,
        Down = Key.Down,
        Jump = Key.Kp0,
        Attack1 = Key.Kp1,
        Attack2 = Key.Kp2,
    };

    /// <summary>Reads the keys that are held now.</summary>
    public InputFlags Read()
    {
        InputFlags flags = InputFlags.None;
        if (Input.IsPhysicalKeyPressed(Left)) flags |= InputFlags.Left;
        if (Input.IsPhysicalKeyPressed(Right)) flags |= InputFlags.Right;
        if (Input.IsPhysicalKeyPressed(Up)) flags |= InputFlags.Up;
        if (Input.IsPhysicalKeyPressed(Down)) flags |= InputFlags.Down;
        if (Input.IsPhysicalKeyPressed(Jump)) flags |= InputFlags.Jump;
        if (Input.IsPhysicalKeyPressed(Attack1)) flags |= InputFlags.Attack1;
        if (Input.IsPhysicalKeyPressed(Attack2)) flags |= InputFlags.Attack2;
        return flags;
    }
}
