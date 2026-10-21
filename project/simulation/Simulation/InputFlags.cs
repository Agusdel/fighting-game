using System;
using System.Runtime.CompilerServices;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>The buttons of one player for one frame.</summary>
/// <remarks>
/// Each button is one bit, from bit 0 up, with no gaps. When you add a button, update
/// <see cref="InputFlagsExtensions.ButtonCount"/> (a unit test checks it).
/// </remarks>
[Flags]
public enum InputFlags : ushort
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Up = 1 << 2,
    Down = 1 << 3,
    Jump = 1 << 4,

    /// <summary>Light attack: fast and weak.</summary>
    Attack1 = 1 << 5,

    /// <summary>Heavy attack: slow and strong.</summary>
    Attack2 = 1 << 6,

    // Bits 7 to 15 are free for more buttons.
}

public static class InputFlagsExtensions
{
    /// <summary>
    /// The number of buttons in <see cref="InputFlags"/> (bits 0 to ButtonCount - 1). A constant, because inline arrays
    /// (for example <see cref="InputBufferData"/>) need their length at compile time.
    /// </summary>
    public const int ButtonCount = 7;

    /// <summary>All buttons of <see cref="InputFlags"/>.</summary>
    public const InputFlags AllButtons = (InputFlags)((1 << ButtonCount) - 1);

    public static bool Has(this InputFlags bits, InputFlags flag) => (bits & flag) != 0;

    /// <summary>Returns -1 (left), +1 (right), or 0 (none, or both at the same time).</summary>
    public static int HorizontalDirection(this InputFlags bits) =>
        (bits.Has(InputFlags.Right) ? 1 : 0) - (bits.Has(InputFlags.Left) ? 1 : 0);
}

/// <summary>The inputs of all players for one frame. Index = player slot.</summary>
[InlineArray(GameConstants.MaxPlayers)]
public struct FrameInput
{
    private InputFlags _element0;
}
