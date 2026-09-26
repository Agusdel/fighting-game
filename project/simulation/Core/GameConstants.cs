namespace FightingGame.Core;

/// <summary>
/// Global constants shared by the simulation, the session, and the presentation.
/// </summary>
public static class GameConstants
{
    /// <summary>Maximum number of fighters in one match (all machines together).</summary>
    public const int MaxPlayers = 4;

    /// <summary>Simulation ticks per second.</summary>
    public const int TickRate = 60;
}
