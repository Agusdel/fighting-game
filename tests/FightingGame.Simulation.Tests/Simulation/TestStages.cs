using FightingGame.Core;
using FightingGame.Simulation;

namespace FightingGame.Simulation.Tests.Simulation;

/// <summary>Stages for tests. The tests cannot load Godot scenes, so the stages are built in code.</summary>
internal static class TestStages
{
    /// <summary>
    /// A closed 1152 x 648 box: floor (top at y = 600), ceiling, two walls, and one platform (x 426–726, top at y = 420).
    /// One single spawn position on the platform, two spawn position pairs on the floor.
    /// It is a copy of Stage01.tscn, in the same order.
    /// </summary>
    public static StageData CreateDefault()
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

        FixedVector2[] singleSpawnPositions =
        {
            new(576, 420),
        };

        SpawnPositionPair[] spawnPositionPairs =
        {
            new(new FixedVector2(250, 600), new FixedVector2(902, 600)),
            new(new FixedVector2(450, 600), new FixedVector2(702, 600)),
        };

        return new StageData(solids, platforms, singleSpawnPositions, spawnPositionPairs);
    }

    /// <summary>Game data with <see cref="CreateDefault"/> and the default fighter stats.</summary>
    public static GameData CreateGameData() => new()
    {
        Stage = CreateDefault(),
        Fighter = DefaultGameData.CreateFighterStats(),
    };
}
