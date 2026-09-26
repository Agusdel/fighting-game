using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Static data for a match. It does not change during the match, so it is not part of the rollback state.
/// All machines must load the same data.
/// </summary>
public sealed class GameData
{
    public required StageData Stage { get; init; }
    public required FighterStats Fighter { get; init; }
}

/// <summary>
/// Stage geometry. The array order must be the same on every machine (for example, the scene tree order).
/// The collision code does not depend on the order, but a fixed order keeps the data identical everywhere.
/// </summary>
public sealed class StageData
{
    /// <summary>Boxes that block from all sides (floor, ceiling, walls).</summary>
    public FixedAABB[] Solids { get; }

    /// <summary>One-way platforms. They block only a fighter that falls onto their top edge.</summary>
    public FixedAABB[] Platforms { get; }

    /// <summary>Start positions (feet), one for each player slot.</summary>
    public FixedVector2[] SpawnPoints { get; }

    /// <summary>The box that contains all solids and platforms.</summary>
    public FixedAABB Bounds { get; }

    public StageData(FixedAABB[] solids, FixedAABB[] platforms, FixedVector2[] spawnPoints)
    {
        if (solids.Length == 0)
        {
            throw new ArgumentException("A stage needs at least one solid.", nameof(solids));
        }

        Solids = solids;
        Platforms = platforms;
        SpawnPoints = spawnPoints;

        FixedVector2 min = solids[0].Min;
        FixedVector2 max = solids[0].Max;
        foreach (FixedAABB box in solids)
        {
            (min, max) = Grow(min, max, box);
        }
        foreach (FixedAABB box in platforms)
        {
            (min, max) = Grow(min, max, box);
        }
        Bounds = new FixedAABB(min, max);
    }

    private static (FixedVector2, FixedVector2) Grow(FixedVector2 min, FixedVector2 max, FixedAABB box) =>
        (new FixedVector2(Fixed.Min(min.X, box.Min.X), Fixed.Min(min.Y, box.Min.Y)),
         new FixedVector2(Fixed.Max(max.X, box.Max.X), Fixed.Max(max.Y, box.Max.Y)));
}

/// <summary>Movement values of a fighter. Speeds are in pixels per frame, accelerations in pixels per frame².</summary>
public sealed class FighterStats
{
    /// <summary>Width and height of the collision box. The box is centered on the feet on X and goes up from the feet on Y. Use an even width.</summary>
    public required FixedVector2 CollisionBoxSize { get; init; }

    public required Fixed WalkSpeed { get; init; }

    /// <summary>Maximum horizontal speed in the air.</summary>
    public required Fixed AirSpeed { get; init; }

    /// <summary>Horizontal acceleration in the air while a direction is held.</summary>
    public required Fixed AirAcceleration { get; init; }

    /// <summary>Horizontal deceleration in the air while no direction is held.</summary>
    public required Fixed AirFriction { get; init; }

    public required Fixed Gravity { get; init; }
    public required Fixed MaxFallSpeed { get; init; }

    /// <summary>Vertical velocity at the start of a jump. Negative, because Y points down.</summary>
    public required Fixed JumpVelocity { get; init; }

    /// <summary>Frames on the ground before a ground jump starts.</summary>
    public required int JumpSquatFrames { get; init; }

    /// <summary>Frames of <see cref="FighterAction.Land"/> after landing.</summary>
    public required int LandFrames { get; init; }

    /// <summary>Total jumps before landing again (1 ground jump + air jumps).</summary>
    public required byte MaxJumps { get; init; }

    /// <summary>Frames the fighter ignores one-way platforms after a drop-through.</summary>
    public required int DropThroughFrames { get; init; }

    /// <summary>The collision box for a fighter with its feet at <paramref name="feet"/>.</summary>
    public FixedAABB CollisionBoxAt(FixedVector2 feet)
    {
        Fixed halfWidth = CollisionBoxSize.X * Fixed.Half;
        return new FixedAABB(
            new FixedVector2(feet.X - halfWidth, feet.Y - CollisionBoxSize.Y),
            new FixedVector2(feet.X + halfWidth, feet.Y));
    }
}
