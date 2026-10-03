namespace FightingGame.App;

/// <summary>How a match runs its simulation.</summary>
public enum MatchSessionType
{
    /// <summary>All players on this machine. Each frame runs once.</summary>
    Local,

    /// <summary>
    /// Debug: all players on this machine, and after each frame the session rolls back and runs the last frames again.
    /// It stops the match with a report when a frame gives a different state the second time.
    /// </summary>
    SyncTest,
}
