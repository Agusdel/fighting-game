using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Hard-coded game data that has no editor authoring yet. Stages come from stage scenes made in the Godot editor.
/// </summary>
public static class DefaultGameData
{
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
