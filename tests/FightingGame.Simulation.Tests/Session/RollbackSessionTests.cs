using System;
using System.Linq;
using FightingGame.Networking;
using FightingGame.Session;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class RollbackSessionTests
{
    [Fact]
    public void StartsWithTheSameWorldAsWorldDataCreate()
    {
        var match = new LoopbackMatch(new[] { 0, 1 });
        RollbackSession session = match.Sessions[0];

        Assert.Equal(WorldData.Create(match.Data, 2, 77).ComputeHash(), session.World.ComputeHash());
        Assert.Equal(0, session.ConfirmedFrame);
        Assert.Equal(1, session.ConfirmedInputFrame);   // Frames 0 and 1 (input delay 2) have empty inputs.
        Assert.Equal(session.World.ComputeHash(), session.ConfirmedFrameHash);
    }

    [Fact]
    public void WithoutLatencyNoRollbackAndNoWaitOccurs()
    {
        var match = new LoopbackMatch(new[] { 0, 1 });
        match.Run(600);

        foreach (RollbackSession session in match.Sessions)
        {
            Assert.Equal(600, session.World.Frame);
            Assert.Equal(0, session.Stats.Rollbacks);
            Assert.Equal(0, session.Stats.PredictionWaits);
            Assert.Equal(0, session.Stats.TimeSyncWaits);
            Assert.Equal(600 / 30, session.Stats.HashesCompared);   // Frames 0, 30, ..., 570 (frame 600 is not confirmed yet).
        }
        match.CheckConfirmedFramesAgainstReference();
    }

    [Theory]
    [InlineData(new[] { 0, 1 }, 50, 0, 0)]
    [InlineData(new[] { 0, 1 }, 40, 30, 20)]
    [InlineData(new[] { 0, 1, 2 }, 40, 30, 20)]
    [InlineData(new[] { 0, 1, 2, 3 }, 40, 30, 20)]
    [InlineData(new[] { 0, 0, 1, 1 }, 40, 30, 20)]   // Two local players on each peer.
    [InlineData(new[] { 0, 1, 1 }, 100, 50, 10)]     // High latency: the peers often wait at the max prediction.
    public void WithLatencyAndLossAllPeersConfirmTheSameFrames(int[] slotOwners, int latencyMs, int jitterMs, int lossPercent)
    {
        var match = new LoopbackMatch(slotOwners, latencyMs, jitterMs, lossPercent);
        match.Run(2000);

        WorldData reference = match.CheckConfirmedFramesAgainstReference();

        Assert.True(reference.Round > 3, $"The bots must end some rounds, or the test proves less (round {reference.Round}).");
        foreach (RollbackSession session in match.Sessions)
        {
            Assert.True(session.Stats.Rollbacks > 0, $"{session.LocalPeer}: no rollback occurred, so the test does not check the rollback path.");
            Assert.True(session.Stats.LongestRollback <= 8);
            Assert.True(session.World.Frame - session.ConfirmedInputFrame <= 9);
            Assert.True(session.Stats.HashesCompared >= (slotOwners.Max()) * 2000 / 30 / 2,
                $"{session.LocalPeer}: only {session.Stats.HashesCompared} hashes compared.");
            Assert.True(Math.Abs(session.World.Frame - match.Sessions[0].World.Frame) <= 3);
        }
    }

    [Fact]
    public void PredictionRepeatsTheLastConfirmedInput()
    {
        // Slot 1 (peer 1) holds Right on all frames, slot 0 (peer 0) holds nothing.
        var match = new LoopbackMatch(new[] { 0, 1 }, latencyMs: 50,
            inputSource: (int slot, in WorldData world) => slot == 1 ? InputFlags.Right : InputFlags.None);
        match.Run(300);

        // Peer 0 predicts "nothing" for slot 1 until the first input of slot 1 arrives: one rollback.
        // After that, the prediction (the last input: Right) is always correct.
        Assert.Equal(1, match.Sessions[0].Stats.Rollbacks);
        Assert.Equal(0, match.Sessions[1].Stats.Rollbacks);
        match.CheckConfirmedFramesAgainstReference();
    }

    [Fact]
    public void FullLossStopsThePeersAtTheMaxPrediction()
    {
        var match = new LoopbackMatch(new[] { 0, 1 });
        match.Run(100);

        match.Network.LossPercent = 100;
        match.Run(60);
        int[] stoppedFrames = match.Sessions.Select(s => s.World.Frame).ToArray();
        match.Run(30);

        foreach (RollbackSession session in match.Sessions)
        {
            Assert.Equal(stoppedFrames[session.LocalPeer.Value], session.World.Frame);
            Assert.Equal(session.ConfirmedInputFrame + 8 + 1, session.World.Frame);
            Assert.True(session.Stats.PredictionWaits >= 30);
        }

        match.Network.LossPercent = 0;
        match.Run(100);
        foreach (RollbackSession session in match.Sessions)
        {
            Assert.True(session.World.Frame > stoppedFrames[session.LocalPeer.Value] + 50);
        }
        match.CheckConfirmedFramesAgainstReference();
    }

    [Fact]
    public void TimeSyncSlowsDownAFasterPeer()
    {
        // Peer 1 has a faster clock: it advances one extra frame every 20 ticks (5 % faster).
        var match = new LoopbackMatch(new[] { 0, 1 }, latencyMs: 30);
        match.Run(3000, (tick, peer) => peer == 1 && tick % 20 == 0 ? 2 : 1);

        RollbackSession normal = match.Sessions[0];
        RollbackSession faster = match.Sessions[1];
        // 150 extra frames: the faster peer must wait about as often.
        Assert.InRange(faster.Stats.TimeSyncWaits, 130, 170);
        Assert.Equal(0, normal.Stats.TimeSyncWaits);
        Assert.InRange(faster.World.Frame - normal.World.Frame, -2, 2);
        // Without time sync, the faster peer would run ahead until the max prediction stops it on most ticks.
        Assert.True(faster.Stats.PredictionWaits < 10, $"{faster.Stats.PredictionWaits} prediction waits.");
        match.CheckConfirmedFramesAgainstReference();
    }

    [Fact]
    public void TimeSyncCorrectsALateStart()
    {
        // Peer 1 starts 30 ticks after peer 0. Peer 0 runs to the max prediction, then time sync makes it wait.
        var match = new LoopbackMatch(new[] { 0, 1 }, latencyMs: 30);
        match.Run(600, (tick, peer) => peer == 1 && tick < 30 ? 0 : 1);

        RollbackSession early = match.Sessions[0];
        RollbackSession late = match.Sessions[1];
        Assert.True(early.Stats.TimeSyncWaits > 0);
        Assert.InRange(early.World.Frame - late.World.Frame, -2, 2);
        match.CheckConfirmedFramesAgainstReference();
    }

    [Fact]
    public void DifferentGameDataIsADesync()
    {
        // Peer 1 has a different restart delay. The states differ only after the first round ends.
        GameData peer1Data = LoopbackMatch.CreateGameData();
        peer1Data = new GameData { Stage = peer1Data.Stage, FighterDefinition = peer1Data.FighterDefinition, Rules = new MatchRulesData { RestartDelayFrames = 31 } };
        var match = new LoopbackMatch(new[] { 0, 1 }, latencyMs: 40, jitterMs: 30, lossPercent: 20, peer1Data: peer1Data);

        DesyncException desync = Assert.Throws<DesyncException>(() => match.Run(3000));

        Assert.True(desync.Frame > 0, "The states are the same until the first round ends.");
        Assert.Equal(0, desync.Frame % 30);
        Assert.NotEqual(desync.LocalHash, desync.RemoteHash);
        Assert.Contains($"at frame {desync.Frame}", desync.Message);
        // The session that found the desync throws the same exception again on each call.
        RollbackSession detector = match.Sessions[1 - desync.Peer.Value];
        Assert.Same(desync, Assert.Throws<DesyncException>(() => detector.AdvanceFrame()));
    }

    [Fact]
    public void ADifferentStartStateIsADesyncAtFrame0()
    {
        GameData peer1Data = LoopbackMatch.CreateGameData();
        peer1Data = new GameData
        {
            Stage = peer1Data.Stage,
            FighterDefinition = DefaultGameData.CreateFighterDefinition(DefaultGameData.CreateFighterStats() with { MaxHealth = 11 }),
            Rules = peer1Data.Rules,
        };
        var match = new LoopbackMatch(new[] { 0, 1 }, peer1Data: peer1Data);

        DesyncException desync = Assert.Throws<DesyncException>(() => match.Run(10));

        Assert.Equal(0, desync.Frame);
    }

    [Fact]
    public void IgnoresInvalidMessages()
    {
        var match = new LoopbackMatch(new[] { 0, 1, 2 });
        INetworkTransport otherPeer = match.Network.Transport(1);
        otherPeer.Send(new PeerId(0), new byte[] { 99, 1, 2 }, DeliveryMode.Unreliable);   // Unknown type.
        otherPeer.Send(new PeerId(0), new byte[] { (byte)MessageType.Input, 1 }, DeliveryMode.Unreliable);   // Too short.

        // An input message with the slots of another peer.
        var message = new InputMessage { StartFrame = 2, FrameCount = 1, SlotMask = 0b100 };
        var writer = new MessageWriter(new byte[InputMessage.MaxSize]);
        message.Write(ref writer);
        otherPeer.Send(new PeerId(0), writer.Written, DeliveryMode.Unreliable);

        match.Run(10);

        Assert.Equal(3, match.Sessions[0].Stats.InvalidMessages);
        Assert.Equal(0, match.Sessions[1].Stats.InvalidMessages);
        match.CheckConfirmedFramesAgainstReference();
    }

    [Fact]
    public void RejectsInputForARemoteSlot()
    {
        var match = new LoopbackMatch(new[] { 0, 0, 1 });
        RollbackSession session = match.Sessions[0];
        session.SetLocalInput(1, InputFlags.Jump);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(2, InputFlags.Jump));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(3, InputFlags.Jump));
        Assert.True(session.IsLocalSlot(0));
        Assert.False(session.IsLocalSlot(2));
    }

    [Fact]
    public void RejectsInvalidSetups()
    {
        var network = new LoopbackNetwork(2, 1);
        GameData data = LoopbackMatch.CreateGameData();
        RollbackSessionSetup Setup(int[] owners, int inputDelay = 2, int maxPrediction = 8) => new()
        {
            Seed = 1,
            SlotOwners = owners.Select(o => new PeerId(o)).ToArray(),
            InputDelay = inputDelay,
            MaxPrediction = maxPrediction,
        };

        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 1, 1 }), network.Transport(0)));   // No local slot.
        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 0, 1, 1, 1, 1 }), network.Transport(0)));
        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 0, 1 }, inputDelay: -1), network.Transport(0)));
        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 0, 1 }, inputDelay: 11), network.Transport(0)));
        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 0, 1 }, maxPrediction: 0), network.Transport(0)));
        Assert.Throws<ArgumentException>(() => new RollbackSession(data, Setup(new[] { 0, 1 }, maxPrediction: 17), network.Transport(0)));
        Assert.Throws<ArgumentException>(() => new RollbackSession(data,
            new RollbackSessionSetup { Seed = 1, SlotOwners = new[] { new PeerId(0), new PeerId(1) }, HashInterval = 0 }, network.Transport(0)));
    }
}
