using FightingGame.Core;
using FightingGame.Simulation;

namespace FightingGame.Simulation.Tests.Session;

internal static class TestBot
{
    /// <summary>
    /// A simple test bot: it walks to the nearest other fighter, and near it presses random buttons
    /// (attacks, jumps, directions). With random inputs only, the fighters seldom meet.
    /// </summary>
    public static InputFlags Input(in WorldData world, int slot, ref FixedRng rng)
    {
        FighterData self = world.Fighters[slot];
        Fixed? nearestDx = null;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            FighterData other = world.Fighters[i];
            if (i == slot || !other.Active || other.Health <= 0)
            {
                continue;
            }
            Fixed dx = other.Position.X - self.Position.X;
            if (nearestDx == null || Fixed.Abs(dx) < Fixed.Abs(nearestDx.Value))
            {
                nearestDx = dx;
            }
        }

        if (nearestDx is { } distance && Fixed.Abs(distance) > Fixed.FromInt(60))
        {
            InputFlags walk = distance > Fixed.Zero ? InputFlags.Right : InputFlags.Left;
            return rng.NextInt(30) == 0 ? walk | InputFlags.Jump : walk;
        }
        return (InputFlags)rng.NextInt(1 << 7);
    }
}
