using System;

namespace FightingGame.Networking;

/// <summary>One machine in a match.</summary>
public readonly record struct PeerId(int Value)
{
    public override string ToString() => $"Peer {Value}";
}

public enum DeliveryMode : byte
{
    /// <summary>The message can be lost, and messages can arrive in a different order.</summary>
    Unreliable,

    /// <summary>The message always arrives, and messages from one peer arrive in the order they were sent.</summary>
    Reliable,
}

/// <summary>Receives one message. <paramref name="data"/> is valid only during the call: copy it to keep it.</summary>
public delegate void MessageHandler(PeerId from, ReadOnlySpan<byte> data);

/// <summary>
/// Sends and receives messages between the peers of a match. The session code uses only this interface,
/// so a new network (for example Steam) needs only a new transport.
/// </summary>
/// <remarks>
/// Messages arrive only in <see cref="Poll"/>, on the thread that calls it. Connection, host, and join are added
/// with the first real network transport.
/// </remarks>
public interface INetworkTransport
{
    PeerId LocalPeer { get; }

    /// <summary>Sends a copy of <paramref name="data"/>. The caller can reuse its buffer after the call.</summary>
    void Send(PeerId peer, ReadOnlySpan<byte> data, DeliveryMode mode);

    /// <summary>Raises <see cref="MessageReceived"/> for each message that has arrived since the last call.</summary>
    void Poll();

    event MessageHandler? MessageReceived;
}
