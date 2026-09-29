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
    public required FighterDefinitionData FighterDefinition { get; init; }
    public MatchRulesData Rules { get; init; } = new();
}

/// <summary>Match settings. Part of the static game data, so all players of an online match use the same rules.</summary>
public sealed class MatchRulesData
{
    /// <summary>When a hit connects, the attacker and the target freeze for the hitstop frames of the hitbox.</summary>
    public bool HitstopEnabled { get; init; } = true;

    /// <summary>Frames between the end of a round (at most one fighter left) and the start of the next round.</summary>
    public int RestartDelayFrames { get; init; } = 120;

    public ulong ComputeHash()
    {
        var hasher = new StateHasher();
        hasher.Add(HitstopEnabled);
        hasher.Add(RestartDelayFrames);
        return hasher.Value;
    }
}

/// <summary>Two spawn positions (feet) at opposite places on the stage. They do not need to be exact mirrors.</summary>
public readonly struct SpawnPositionPair
{
    public readonly FixedVector2 A;
    public readonly FixedVector2 B;

    public SpawnPositionPair(FixedVector2 a, FixedVector2 b)
    {
        A = a;
        B = b;
    }
}

/// <summary>
/// Stage geometry and spawn positions. The array order must be the same on every machine (for example, the scene tree order).
/// The collision code does not depend on the order, but the spawn position selection does, and <see cref="ComputeHash"/> does too.
/// </summary>
public sealed class StageData
{
    /// <summary>The minimum number of spawn position pairs: enough pairs for <see cref="GameConstants.MaxPlayers"/> players.</summary>
    public const int MinSpawnPositionPairs = GameConstants.MaxPlayers / 2;

    /// <summary>Boxes that block from all sides (floor, ceiling, walls).</summary>
    public FixedAABB[] Solids { get; }

    /// <summary>One-way platforms. They block only a fighter that falls onto their top edge.</summary>
    public FixedAABB[] Platforms { get; }

    /// <summary>Spawn positions (feet) that are not in a pair. When the number of players is odd, a match uses one of them, chosen at random.</summary>
    public FixedVector2[] SingleSpawnPositions { get; }

    /// <summary>Pairs of opposite spawn positions. A match uses (player count / 2) random pairs.</summary>
    public SpawnPositionPair[] SpawnPositionPairs { get; }

    /// <summary>The box that contains all solids and platforms.</summary>
    public FixedAABB Bounds { get; }

    public StageData(FixedAABB[] solids, FixedAABB[] platforms, FixedVector2[] singleSpawnPositions, SpawnPositionPair[] spawnPositionPairs)
    {
        if (solids.Length == 0)
        {
            throw new ArgumentException("A stage needs at least one solid.", nameof(solids));
        }
        if (singleSpawnPositions.Length == 0)
        {
            throw new ArgumentException("A stage needs at least one single spawn position (for an odd number of players).", nameof(singleSpawnPositions));
        }
        if (spawnPositionPairs.Length < MinSpawnPositionPairs)
        {
            throw new ArgumentException(
                $"A stage needs at least {MinSpawnPositionPairs} spawn position pairs, but it has {spawnPositionPairs.Length}.",
                nameof(spawnPositionPairs));
        }

        Solids = solids;
        Platforms = platforms;
        SingleSpawnPositions = singleSpawnPositions;
        SpawnPositionPairs = spawnPositionPairs;

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

    /// <summary>
    /// Hash of all stage data. Two machines with the same hash have the same stage.
    /// The array lengths are included, so data cannot move from one array to the next without a change.
    /// </summary>
    public ulong ComputeHash()
    {
        var hasher = new StateHasher();
        hasher.Add(Solids.Length);
        foreach (FixedAABB box in Solids)
        {
            hasher.Add(box);
        }
        hasher.Add(Platforms.Length);
        foreach (FixedAABB box in Platforms)
        {
            hasher.Add(box);
        }
        hasher.Add(SingleSpawnPositions.Length);
        foreach (FixedVector2 position in SingleSpawnPositions)
        {
            hasher.Add(position);
        }
        hasher.Add(SpawnPositionPairs.Length);
        foreach (SpawnPositionPair pair in SpawnPositionPairs)
        {
            hasher.Add(pair.A);
            hasher.Add(pair.B);
        }
        return hasher.Value;
    }

    private static (FixedVector2, FixedVector2) Grow(FixedVector2 min, FixedVector2 max, FixedAABB box) =>
        (new FixedVector2(Fixed.Min(min.X, box.Min.X), Fixed.Min(min.Y, box.Min.Y)),
         new FixedVector2(Fixed.Max(max.X, box.Max.X), Fixed.Max(max.Y, box.Max.Y)));
}

/// <summary>Movement values of a fighter. Speeds are in pixels per frame, accelerations in pixels per frame².</summary>
public sealed record FighterStats
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


    /// <summary>Total jumps before landing again (1 ground jump + air jumps).</summary>
    public required byte MaxJumps { get; init; }

    /// <summary>Frames the fighter ignores one-way platforms after a drop-through.</summary>
    public required int DropThroughFrames { get; init; }

    public required int MaxHealth { get; init; }

    /// <summary>Horizontal deceleration on the ground while the fighter has no control (knockback).</summary>
    public required Fixed GroundFriction { get; init; }

    /// <summary>Adds every value. When you add a property to this class, add it here too.</summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(CollisionBoxSize);
        hasher.Add(WalkSpeed);
        hasher.Add(AirSpeed);
        hasher.Add(AirAcceleration);
        hasher.Add(AirFriction);
        hasher.Add(Gravity);
        hasher.Add(MaxFallSpeed);
        hasher.Add(JumpVelocity);
        hasher.Add(MaxJumps);
        hasher.Add(DropThroughFrames);
        hasher.Add(MaxHealth);
        hasher.Add(GroundFriction);
    }

    /// <summary>The collision box for a fighter with its feet at <paramref name="feet"/>.</summary>
    public FixedAABB CollisionBoxAt(FixedVector2 feet)
    {
        Fixed halfWidth = CollisionBoxSize.X * Fixed.Half;
        return new FixedAABB(
            new FixedVector2(feet.X - halfWidth, feet.Y - CollisionBoxSize.Y),
            new FixedVector2(feet.X + halfWidth, feet.Y));
    }
}
