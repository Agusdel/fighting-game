using System;
using FightingGame.Core;
using FightingGame.Networking;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>
/// Debug: all peers of an online match in one process. Each player slot is a peer with its own
/// <see cref="RollbackSession"/>, and the peers talk through a <see cref="LoopbackNetwork"/> with simulated latency,
/// jitter, and loss. The game shows the world of one peer (<see cref="ShownPeer"/>), so the effect of the network
/// (input delay, rollback corrections of the other players) is visible on one machine.
/// </summary>
/// <remarks>
/// Each <see cref="AdvanceFrame"/> is one tick: the network time advances by one tick (1000 / 60 ms), then each
/// peer advances one frame or waits. The game calls it at the tick rate, so the network time follows the real time.
/// </remarks>
public sealed class LoopbackMatchSession : IMatchSession
{
    private readonly LoopbackNetwork _network;
    private readonly RollbackSession[] _peers;

    /// <summary>Thousandths of a millisecond not yet given to the network.</summary>
    private long _timeRemainder;

    public LoopbackMatchSession(GameData data, int playerCount, ulong seed, int latencyMs, int jitterMs, int lossPercent)
    {
        if (playerCount < 2 || playerCount > GameConstants.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), $"A loopback match needs 2 to {GameConstants.MaxPlayers} players.");
        }

        Data = data;
        _network = new LoopbackNetwork(playerCount, seed)
        {
            LatencyMs = latencyMs,
            JitterMs = jitterMs,
            LossPercent = lossPercent,
        };

        var slotOwners = new PeerId[playerCount];
        for (int slot = 0; slot < playerCount; slot++)
        {
            slotOwners[slot] = new PeerId(slot);
        }
        var setup = new RollbackSessionSetup { Seed = seed, SlotOwners = slotOwners };

        _peers = new RollbackSession[playerCount];
        for (int peer = 0; peer < playerCount; peer++)
        {
            _peers[peer] = new RollbackSession(data, setup, _network.Transport(peer));
        }
    }

    public GameData Data { get; }

    /// <summary>The world of the shown peer.</summary>
    public ref readonly WorldData World => ref ShownSession.World;

    public int PeerCount => _peers.Length;

    /// <summary>The peer whose world the game shows. Peer i owns player slot i.</summary>
    public int ShownPeer { get; private set; }

    public RollbackSession ShownSession => _peers[ShownPeer];

    public LoopbackNetwork Network => _network;

    public void ShowNextPeer() => ShownPeer = (ShownPeer + 1) % _peers.Length;

    public void SetLocalInput(int slot, InputFlags input)
    {
        if (slot < 0 || slot >= _peers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} is not in the match ({_peers.Length} players).");
        }
        _peers[slot].SetLocalInput(slot, input);
    }

    /// <summary>Advances the network time and each peer. Returns true if the shown peer ran a frame.</summary>
    /// <exception cref="DesyncException">A peer found a desync.</exception>
    public bool AdvanceFrame()
    {
        _timeRemainder += 1_000_000 / GameConstants.TickRate;
        _network.AdvanceTime(_timeRemainder / 1000);
        _timeRemainder %= 1000;

        bool shownPeerRan = false;
        for (int peer = 0; peer < _peers.Length; peer++)
        {
            bool ran = _peers[peer].AdvanceFrame();
            shownPeerRan |= ran && peer == ShownPeer;
        }
        return shownPeerRan;
    }
}
