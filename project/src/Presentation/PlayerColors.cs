using System.Collections.Generic;
using Godot;

namespace FightingGame.Presentation;

/// <summary>The color of each player slot, shared by the lobby, the fighter views, and the HUD.</summary>
public static class PlayerColors
{
    private static readonly Color[] Colors =
    {
        new(0.9f, 0.3f, 0.3f),
        new(0.3f, 0.5f, 0.95f),
        new(0.3f, 0.85f, 0.4f),
        new(0.95f, 0.8f, 0.2f),
    };

    public static IReadOnlyList<Color> All => Colors;

    /// <summary>The color of a slot. Slots beyond the color list reuse the colors from the start.</summary>
    public static Color Of(int slot) => Colors[slot % Colors.Length];
}
