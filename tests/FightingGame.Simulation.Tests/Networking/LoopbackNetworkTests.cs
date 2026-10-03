using System;
using System.Collections.Generic;
using System.Linq;
using FightingGame.Networking;
using Xunit;

namespace FightingGame.Simulation.Tests.Networking;

public class LoopbackNetworkTests
{
    private static readonly PeerId Peer0 = new(0);
    private static readonly PeerId Peer1 = new(1);

    /// <summary>Collects the messages that a transport receives: the sender and the first byte.</summary>
    private sealed class Inbox
    {
        public readonly List<(PeerId From, byte Value)> Messages = new();

        public Inbox(INetworkTransport transport)
        {
            transport.MessageReceived += (from, data) => Messages.Add((from, data[0]));
        }

        public byte[] Values => Messages.Select(m => m.Value).ToArray();
    }

    private static void SendBytes(INetworkTransport transport, PeerId to, int count, DeliveryMode mode)
    {
        for (int i = 0; i < count; i++)
        {
            transport.Send(to, new[] { (byte)i }, mode);
        }
    }

    [Fact]
    public void WithNoLatencyAMessageArrivesAtTheNextPoll()
    {
        var network = new LoopbackNetwork(2, 1);
        var inbox = new Inbox(network.Transport(1));

        network.Transport(0).Send(Peer1, new byte[] { 7 }, DeliveryMode.Unreliable);
        network.Transport(1).Poll();

        Assert.Equal((Peer0, (byte)7), Assert.Single(inbox.Messages));
        Assert.Equal(Peer1, network.Transport(1).LocalPeer);
    }

    [Fact]
    public void AMessageArrivesAfterTheLatency()
    {
        var network = new LoopbackNetwork(2, 1) { LatencyMs = 50 };
        var inbox = new Inbox(network.Transport(1));

        network.Transport(0).Send(Peer1, new byte[] { 7 }, DeliveryMode.Unreliable);
        network.AdvanceTime(49);
        network.Transport(1).Poll();
        Assert.Empty(inbox.Messages);

        network.AdvanceTime(1);
        network.Transport(1).Poll();
        Assert.Single(inbox.Messages);
        Assert.Equal(0, network.PendingCount(Peer1));
    }

    [Fact]
    public void MessagesGoOnlyToTheReceiver()
    {
        var network = new LoopbackNetwork(3, 1);
        var inbox1 = new Inbox(network.Transport(1));
        var inbox2 = new Inbox(network.Transport(2));

        network.Transport(0).Send(Peer1, new byte[] { 1 }, DeliveryMode.Unreliable);
        network.Transport(2).Send(Peer1, new byte[] { 2 }, DeliveryMode.Unreliable);
        network.Transport(1).Poll();
        network.Transport(2).Poll();

        Assert.Equal(new[] { (Peer0, (byte)1), (new PeerId(2), (byte)2) }, inbox1.Messages);
        Assert.Empty(inbox2.Messages);
    }

    [Fact]
    public void SendCopiesTheData()
    {
        var network = new LoopbackNetwork(2, 1);
        var inbox = new Inbox(network.Transport(1));
        var buffer = new byte[] { 1 };

        network.Transport(0).Send(Peer1, buffer, DeliveryMode.Unreliable);
        buffer[0] = 2;
        network.Transport(1).Poll();

        Assert.Equal(new byte[] { 1 }, inbox.Values);
    }

    [Fact]
    public void JitterChangesTheOrderOfUnreliableMessages()
    {
        var network = new LoopbackNetwork(2, 3) { LatencyMs = 20, JitterMs = 30 };
        var inbox = new Inbox(network.Transport(1));

        for (int i = 0; i < 50; i++)
        {
            network.Transport(0).Send(Peer1, new[] { (byte)i }, DeliveryMode.Unreliable);
            network.AdvanceTime(5);
            network.Transport(1).Poll();
        }
        network.AdvanceTime(50);
        network.Transport(1).Poll();

        byte[] values = inbox.Values;
        Assert.Equal(Enumerable.Range(0, 50).Select(i => (byte)i), values.Order());   // All arrive.
        Assert.NotEqual(values.Order(), values);                                         // Not in the send order.
    }

    [Fact]
    public void NoMessageArrivesAfterLatencyPlusJitter()
    {
        var network = new LoopbackNetwork(2, 3) { LatencyMs = 20, JitterMs = 30 };
        var inbox = new Inbox(network.Transport(1));
        SendBytes(network.Transport(0), Peer1, 100, DeliveryMode.Unreliable);

        network.AdvanceTime(50);
        network.Transport(1).Poll();

        Assert.Equal(100, inbox.Messages.Count);
    }

    [Fact]
    public void ReliableMessagesKeepTheirOrderWithJitter()
    {
        var network = new LoopbackNetwork(2, 3) { LatencyMs = 20, JitterMs = 30, LossPercent = 100 };
        var inbox = new Inbox(network.Transport(1));

        for (int i = 0; i < 50; i++)
        {
            network.Transport(0).Send(Peer1, new[] { (byte)i }, DeliveryMode.Reliable);
            network.AdvanceTime(5);
            network.Transport(1).Poll();
        }
        network.AdvanceTime(50);
        network.Transport(1).Poll();

        Assert.Equal(Enumerable.Range(0, 50).Select(i => (byte)i), inbox.Values);   // All arrive, in order, also with 100 % loss.
    }

    [Fact]
    public void FullLossDropsAllUnreliableMessages()
    {
        var network = new LoopbackNetwork(2, 1) { LossPercent = 100 };
        var inbox = new Inbox(network.Transport(1));
        SendBytes(network.Transport(0), Peer1, 20, DeliveryMode.Unreliable);
        network.Transport(1).Poll();
        Assert.Empty(inbox.Messages);
    }

    [Fact]
    public void PartialLossDropsAboutThatPercentage()
    {
        var network = new LoopbackNetwork(2, 5) { LossPercent = 20 };
        var inbox = new Inbox(network.Transport(1));
        SendBytes(network.Transport(0), Peer1, 1000, DeliveryMode.Unreliable);
        network.Transport(1).Poll();
        Assert.InRange(inbox.Messages.Count, 750, 850);
    }

    [Fact]
    public void TheSameSeedGivesTheSameDelivery()
    {
        byte[] Run()
        {
            var network = new LoopbackNetwork(2, 9) { LatencyMs = 10, JitterMs = 40, LossPercent = 30 };
            var inbox = new Inbox(network.Transport(1));
            for (int i = 0; i < 100; i++)
            {
                network.Transport(0).Send(Peer1, new[] { (byte)i }, DeliveryMode.Unreliable);
                network.AdvanceTime(3);
                network.Transport(1).Poll();
            }
            return inbox.Values;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void AHandlerCanSendDuringPoll()
    {
        var network = new LoopbackNetwork(2, 1);
        var inbox0 = new Inbox(network.Transport(0));
        network.Transport(1).MessageReceived += (from, data) =>
            network.Transport(1).Send(from, new[] { (byte)(data[0] + 1) }, DeliveryMode.Unreliable);

        network.Transport(0).Send(Peer1, new byte[] { 1 }, DeliveryMode.Unreliable);
        network.Transport(1).Poll();
        network.Transport(0).Poll();

        Assert.Equal(new byte[] { 2 }, inbox0.Values);
    }

    [Fact]
    public void RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackNetwork(1, 1));
        var network = new LoopbackNetwork(2, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => network.LatencyMs = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => network.JitterMs = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => network.LossPercent = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => network.AdvanceTime(-1));
        Assert.Throws<ArgumentException>(() => network.Transport(0).Send(Peer0, new byte[] { 1 }, DeliveryMode.Unreliable));
        Assert.Throws<ArgumentOutOfRangeException>(() => network.Transport(0).Send(new PeerId(2), new byte[] { 1 }, DeliveryMode.Unreliable));
    }
}
