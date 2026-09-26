using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Hard-coded game data. The stage is temporary: later it is loaded from a stage scene made in the Godot editor.
/// </summary>
public static class DefaultGameData
{
    public static GameData Create() => new()
    {
        Stage = CreateStage(),
        Fighter = CreateFighterStats(),
    };

    /// <summary>
    /// A closed 1152 x 648 box (the default Godot window size): floor, ceiling, two walls, and one platform in the center.
    /// </summary>
    public static StageData CreateStage()
    {
        FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
            new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

        FixedAABB[] solids =
        {
            Box(0, 600, 1152, 648),   // Floor
            Box(0, 0, 1152, 32),      // Ceiling
            Box(0, 32, 32, 600),      // Left wall
            Box(1120, 32, 1152, 600), // Right wall
        };

        FixedAABB[] platforms =
        {
            Box(426, 420, 726, 436),
        };

        FixedVector2[] spawnPoints =
        {
            new(250, 600),
            new(902, 600),
            new(450, 600),
            new(702, 600),
        };

        return new StageData(solids, platforms, spawnPoints);
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
        JumpSquatFrames = 3,
        LandFrames = 3,
        MaxJumps = 2,
        DropThroughFrames = 12,
    };
}
