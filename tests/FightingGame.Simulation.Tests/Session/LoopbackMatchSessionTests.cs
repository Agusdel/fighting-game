using System;
using FightingGame.Core;
using FightingGame.Session;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class LoopbackMatchSessionTests
{
    [Fact]
    public void AllPeersCompareTheirConfirmedHashesWithoutADesync()
    {
        var session = new LoopbackMatchSession(LoopbackMatch.CreateGameData(), 3, seed: 5, latencyMs: 50, jitterMs: 20, lossPercent: 10);
        var rng = new FixedRng(8);

        // Each peer's desync detector compares its confirmed hashes with the other peers: a difference would throw.
        for (int tick = 0; tick < 1200; tick++)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                session.SetLocalInput(slot, TestBot.Input(session.World, slot, ref rng));
            }
            session.AdvanceFrame();
        }

        for (int peer = 0; peer < 3; peer++)
        {
            Assert.Equal(peer, session.ShownPeer);
            Assert.True(session.ShownSession.Stats.Rollbacks > 0);
            Assert.True(session.ShownSession.Stats.HashesCompared >= 2 * 1200 / 30 / 2,
                $"Peer {peer}: only {session.ShownSession.Stats.HashesCompared} hashes compared.");
            Assert.Equal(session.ShownSession.World.Frame, session.World.Frame);
            session.ShowNextPeer();
        }
        Assert.Equal(0, session.ShownPeer);
    }

    [Fact]
    public void InputGoesToThePeerOfTheSlot()
    {
        var session = new LoopbackMatchSession(LoopbackMatch.CreateGameData(), 2, seed: 5, latencyMs: 0, jitterMs: 0, lossPercent: 0);
        for (int tick = 0; tick < 30; tick++)
        {
            session.SetLocalInput(1, InputFlags.Right);
            session.AdvanceFrame();
        }

        session.ShowNextPeer();   // Peer 1 sees its own fighter (slot 1) walk.
        Assert.Equal(InputFlags.Right, session.World.Fighters[1].PrevInput);
        Assert.Equal(InputFlags.None, session.World.Fighters[0].PrevInput);
    }

    [Fact]
    public void RejectsInvalidValues()
    {
        GameData data = LoopbackMatch.CreateGameData();
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackMatchSession(data, 1, 1, 0, 0, 0));
        var session = new LoopbackMatchSession(data, 2, 1, 0, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetLocalInput(2, InputFlags.Jump));
    }
}
