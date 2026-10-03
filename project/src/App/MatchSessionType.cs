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

    /// <summary>
    /// Debug: each player is a separate peer with its own rollback session, in this process. The peers talk through an
    /// in-memory network with simulated latency, jitter, and loss (<see cref="LoopbackSettings"/>). The game shows the
    /// world of one peer, so the effect of the network is visible on one machine. Needs 2 or more players.
    /// </summary>
    Loopback,
}

/// <summary>The simulated network of <see cref="MatchSessionType.Loopback"/>.</summary>
/// <param name="LatencyMs">The time from send to delivery (one way).</param>
/// <param name="JitterMs">A random 0 to this value is added to the latency of each message.</param>
/// <param name="LossPercent">The percentage of unreliable messages that are lost.</param>
public sealed record LoopbackSettings(int LatencyMs, int JitterMs, int LossPercent);
