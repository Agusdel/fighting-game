using System;
using System.Collections.Generic;
using FightingGame.Core;
using FightingGame.Networking;

namespace FightingGame.Session;

/// <summary>The settings of one peer's <see cref="RollbackSession"/>. All peers of a match use the same values.</summary>
public sealed class RollbackSessionSetup
{
    /// <summary>The largest <see cref="InputDelay"/>.</summary>
    public const int MaxInputDelay = 10;

    /// <summary>The largest <see cref="MaxPrediction"/>.</summary>
    public const int MaxMaxPrediction = 16;

    /// <summary>The seed of the match random generator. The same on all peers.</summary>
    public required ulong Seed { get; init; }

    /// <summary>
    /// The peer that owns each player slot (index = slot). The count is the number of players.
    /// A peer can own more than one slot (more than one local player on one machine).
    /// </summary>
    public required IReadOnlyList<PeerId> SlotOwners { get; init; }

    /// <summary>
    /// Frames between a local input and the frame that uses it. A larger delay gives fewer rollbacks,
    /// but the local players feel the delay (2 frames = 33 ms).
    /// </summary>
    public int InputDelay { get; init; } = 2;

    /// <summary>
    /// The largest number of frames that a peer runs with predicted inputs. At this limit, the peer waits for the
    /// remote inputs. It is also the longest rollback.
    /// </summary>
    public int MaxPrediction { get; init; } = 8;

    public int PlayerCount => SlotOwners.Count;

    /// <summary>Throws <see cref="ArgumentException"/> if a value is not valid for the local peer.</summary>
    internal void Validate(PeerId localPeer)
    {
        if (SlotOwners.Count < 1 || SlotOwners.Count > GameConstants.MaxPlayers)
        {
            throw new ArgumentException($"A match needs 1 to {GameConstants.MaxPlayers} player slots, but it has {SlotOwners.Count}.");
        }
        if (InputDelay < 0 || InputDelay > MaxInputDelay)
        {
            throw new ArgumentException($"The input delay must be 0 to {MaxInputDelay}, but it is {InputDelay}.");
        }
        if (MaxPrediction < 1 || MaxPrediction > MaxMaxPrediction)
        {
            throw new ArgumentException($"The max prediction must be 1 to {MaxMaxPrediction}, but it is {MaxPrediction}.");
        }

        bool hasLocalSlot = false;
        foreach (PeerId owner in SlotOwners)
        {
            hasLocalSlot |= owner == localPeer;
        }
        if (!hasLocalSlot)
        {
            throw new ArgumentException($"The local peer ({localPeer}) owns no player slot.");
        }
    }
}
