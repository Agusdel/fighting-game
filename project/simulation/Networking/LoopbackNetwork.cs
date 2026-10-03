using System;
using System.Collections.Generic;
using FightingGame.Core;

namespace FightingGame.Networking;

/// <summary>
/// An in-memory network for sessions in one process (no sockets). It simulates latency, jitter, and packet loss,
/// for tests and for the loopback mode of the game.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The network has its own time (<see cref="TimeMs"/>). Only <see cref="AdvanceTime"/> changes it, so a test
/// controls the time exactly. The game advances it with the real time.</item>
/// <item>A message arrives after <see cref="LatencyMs"/> + a random 0 to <see cref="JitterMs"/>. So unreliable
/// messages can arrive in a different order.</item>
/// <item><see cref="LossPercent"/> of the unreliable messages are lost. Reliable messages are never lost, and messages
/// from one peer to another arrive in the order they were sent.</item>
/// <item>The random values come from a <see cref="FixedRng"/> with a seed, so a run with the same calls is always the same.</item>
/// </list>
/// </remarks>
public sealed class LoopbackNetwork
{
    private readonly struct Packet
    {
        public required PeerId From { get; init; }
        public required long DeliveryTimeMs { get; init; }

        /// <summary>The send order. Packets with the same delivery time arrive in the send order.</summary>
        public required long Sequence { get; init; }
        public required byte[] Data { get; init; }
    }

    private readonly LoopbackTransport[] _transports;

    /// <summary>The packets that are not delivered yet, for each receiving peer.</summary>
    private readonly List<Packet>[] _pending;

    /// <summary>The delivery time of the last reliable packet, for each pair of peers (from * count + to).</summary>
    private readonly long[] _lastReliableDeliveryMs;

    private FixedRng _rng;
    private long _sequence;
    private int _latencyMs;
    private int _jitterMs;
    private int _lossPercent;

    public LoopbackNetwork(int peerCount, ulong seed)
    {
        if (peerCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(peerCount), "A network needs at least 2 peers.");
        }

        _rng = new FixedRng(seed);
        _pending = new List<Packet>[peerCount];
        _transports = new LoopbackTransport[peerCount];
        _lastReliableDeliveryMs = new long[peerCount * peerCount];
        for (int i = 0; i < peerCount; i++)
        {
            _pending[i] = new List<Packet>();
            _transports[i] = new LoopbackTransport(this, new PeerId(i));
        }
    }

    public int PeerCount => _transports.Length;

    /// <summary>The time of the network, in milliseconds from its creation.</summary>
    public long TimeMs { get; private set; }

    /// <summary>The time from send to delivery (one way), without jitter.</summary>
    public int LatencyMs
    {
        get => _latencyMs;
        set => _latencyMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The latency must not be negative.");
    }

    /// <summary>A random 0 to this value is added to the latency of each message.</summary>
    public int JitterMs
    {
        get => _jitterMs;
        set => _jitterMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The jitter must not be negative.");
    }

    /// <summary>The percentage (0 to 100) of unreliable messages that are lost.</summary>
    public int LossPercent
    {
        get => _lossPercent;
        set => _lossPercent = value is >= 0 and <= 100 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The loss must be 0 to 100.");
    }

    /// <summary>The transport of one peer. Peer ids are 0 to <see cref="PeerCount"/> - 1.</summary>
    public LoopbackTransport Transport(int peer) => _transports[peer];

    public void AdvanceTime(long milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Time cannot go back.");
        }
        TimeMs += milliseconds;
    }

    /// <summary>The number of messages to <paramref name="peer"/> that are not delivered yet.</summary>
    public int PendingCount(PeerId peer) => _pending[peer.Value].Count;

    internal void Send(PeerId from, PeerId to, ReadOnlySpan<byte> data, DeliveryMode mode)
    {
        if (to.Value < 0 || to.Value >= PeerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(to), $"{to} is not in the network.");
        }
        if (to == from)
        {
            throw new ArgumentException("A peer cannot send a message to itself.", nameof(to));
        }

        if (mode == DeliveryMode.Unreliable && _lossPercent > 0 && _rng.NextInt(100) < _lossPercent)
        {
            return;
        }

        long deliveryTime = TimeMs + _latencyMs + (_jitterMs > 0 ? _rng.NextInt(_jitterMs + 1) : 0);
        if (mode == DeliveryMode.Reliable)
        {
            // Reliable messages keep their order: never before the previous reliable message on the same path.
            ref long last = ref _lastReliableDeliveryMs[from.Value * PeerCount + to.Value];
            deliveryTime = Math.Max(deliveryTime, last);
            last = deliveryTime;
        }

        _pending[to.Value].Add(new Packet
        {
            From = from,
            DeliveryTimeMs = deliveryTime,
            Sequence = _sequence++,
            Data = data.ToArray(),
        });
    }

    /// <summary>Removes the packets for <paramref name="to"/> that are due now, in delivery order.</summary>
    internal List<(PeerId From, byte[] Data)> TakeDue(PeerId to)
    {
        List<Packet> pending = _pending[to.Value];
        var due = new List<Packet>();
        pending.RemoveAll(packet =>
        {
            if (packet.DeliveryTimeMs > TimeMs)
            {
                return false;
            }
            due.Add(packet);
            return true;
        });

        due.Sort((a, b) => a.DeliveryTimeMs != b.DeliveryTimeMs
            ? a.DeliveryTimeMs.CompareTo(b.DeliveryTimeMs)
            : a.Sequence.CompareTo(b.Sequence));
        return due.ConvertAll(packet => (packet.From, packet.Data));
    }
}

/// <summary>The transport of one peer on a <see cref="LoopbackNetwork"/>.</summary>
public sealed class LoopbackTransport : INetworkTransport
{
    private readonly LoopbackNetwork _network;

    internal LoopbackTransport(LoopbackNetwork network, PeerId localPeer)
    {
        _network = network;
        LocalPeer = localPeer;
    }

    public PeerId LocalPeer { get; }

    public event MessageHandler? MessageReceived;

    public void Send(PeerId peer, ReadOnlySpan<byte> data, DeliveryMode mode) => _network.Send(LocalPeer, peer, data, mode);

    /// <summary>
    /// Delivers the messages that are due at the current network time. A handler can send messages during the call;
    /// they are delivered in a later call.
    /// </summary>
    public void Poll()
    {
        foreach ((PeerId from, byte[] data) in _network.TakeDue(LocalPeer))
        {
            MessageReceived?.Invoke(from, data);
        }
    }
}
