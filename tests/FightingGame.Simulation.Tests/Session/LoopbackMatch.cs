using System.Collections.Generic;
using System.Linq;
using FightingGame.Core;
using FightingGame.Networking;
using FightingGame.Session;
using FightingGame.Simulation;
using FightingGame.Simulation.Tests.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

/// <summary>
/// A test match: one <see cref="RollbackSession"/> for each peer on a <see cref="LoopbackNetwork"/>. All local slots
/// are played by <see cref="TestBot"/>, or by a given input source. It records the real inputs and the confirmed hashes of each peer, and compares
/// them with a run without rollback.
/// </summary>
internal sealed class LoopbackMatch
{
    /// <summary>Low health and a short restart delay, so the bots end many rounds (the rollbacks also cross round restarts).</summary>
    public static GameData CreateGameData() => new()
    {
        Stage = TestStages.CreateDefault(),
        FighterDefinition = DefaultGameData.CreateFighterDefinition(DefaultGameData.CreateFighterStats() with { MaxHealth = 10 }),
        Rules = new MatchRulesData { RestartDelayFrames = 30 },
    };

    /// <summary>Gives the input of a local slot: (slot, world of the local peer) -> input.</summary>
    public delegate InputFlags InputSource(int slot, in WorldData world);

    private readonly RollbackSessionSetup _setup;
    private readonly InputSource? _inputSource;
    private readonly FixedRng[] _botRngs;

    /// <summary>The real input of each slot for each frame (frame -> input), as the local peer stored it.</summary>
    private readonly Dictionary<int, InputFlags>[] _realInputs;

    /// <summary>The confirmed hashes of each peer (frame -> hash).</summary>
    private readonly Dictionary<int, ulong>[] _confirmedHashes;

    /// <summary>Thousandths of a millisecond not yet given to the network (one tick = 1000 / 60 ms).</summary>
    private long _timeRemainder;

    /// <param name="slotOwners">The peer of each slot, for example { 0, 0, 1 } = peer 0 has slots 0 and 1, peer 1 has slot 2.</param>
    public LoopbackMatch(int[] slotOwners, int latencyMs = 0, int jitterMs = 0, int lossPercent = 0, int inputDelay = 2, int maxPrediction = 8,
        InputSource? inputSource = null)
    {
        _inputSource = inputSource;
        Data = CreateGameData();
        int peerCount = slotOwners.Max() + 1;
        Network = new LoopbackNetwork(peerCount, seed: 1234)
        {
            LatencyMs = latencyMs,
            JitterMs = jitterMs,
            LossPercent = lossPercent,
        };
        _setup = new RollbackSessionSetup
        {
            Seed = 77,
            SlotOwners = slotOwners.Select(peer => new PeerId(peer)).ToArray(),
            InputDelay = inputDelay,
            MaxPrediction = maxPrediction,
        };
        Sessions = Enumerable.Range(0, peerCount)
            .Select(peer => new RollbackSession(Data, _setup, Network.Transport(peer)))
            .ToArray();
        _botRngs = Enumerable.Range(0, peerCount).Select(peer => new FixedRng((ulong)(500 + peer))).ToArray();
        _realInputs = slotOwners.Select(_ => new Dictionary<int, InputFlags>()).ToArray();
        _confirmedHashes = Sessions.Select(_ => new Dictionary<int, ulong>()).ToArray();
    }

    public GameData Data { get; }

    public LoopbackNetwork Network { get; }

    public RollbackSession[] Sessions { get; }

    /// <summary>Runs ticks: the network time advances by one tick, then each peer advances one frame (or waits).</summary>
    public void Run(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            _timeRemainder += 1_000_000 / GameConstants.TickRate;
            Network.AdvanceTime(_timeRemainder / 1000);
            _timeRemainder %= 1000;

            for (int peer = 0; peer < Sessions.Length; peer++)
            {
                AdvancePeer(peer);
            }
        }
    }

    private void AdvancePeer(int peer)
    {
        RollbackSession session = Sessions[peer];
        int frame = session.World.Frame;
        var inputs = new Dictionary<int, InputFlags>();
        for (int slot = 0; slot < session.PlayerCount; slot++)
        {
            if (session.IsLocalSlot(slot))
            {
                inputs[slot] = _inputSource != null
                    ? _inputSource(slot, session.World)
                    : TestBot.Input(session.World, slot, ref _botRngs[peer]);
                session.SetLocalInput(slot, inputs[slot]);
            }
        }

        if (session.AdvanceFrame())
        {
            foreach ((int slot, InputFlags input) in inputs)
            {
                _realInputs[slot].Add(frame + _setup.InputDelay, input);
            }
        }

        // A confirmed frame never changes: the same frame must always have the same hash.
        int confirmedFrame = session.ConfirmedFrame;
        ulong hash = session.ConfirmedFrameHash;
        if (_confirmedHashes[peer].TryGetValue(confirmedFrame, out ulong earlierHash))
        {
            Assert.Equal(earlierHash, hash);
        }
        _confirmedHashes[peer][confirmedFrame] = hash;
    }

    /// <summary>
    /// Runs the match again without rollback, with the real inputs, and checks that each confirmed hash of each peer
    /// equals the hash of the same frame. Returns the reference world at the last confirmed frame.
    /// </summary>
    public WorldData CheckConfirmedFramesAgainstReference()
    {
        int lastConfirmed = _confirmedHashes.Min(hashes => hashes.Keys.Max());
        Assert.True(lastConfirmed > 0, "No frame was confirmed.");

        WorldData reference = WorldData.Create(Data, _setup.PlayerCount, _setup.Seed);
        int compared = 0;
        for (int frame = 0; frame < lastConfirmed; frame++)
        {
            FrameInput input = default;
            for (int slot = 0; slot < _setup.PlayerCount; slot++)
            {
                if (frame >= _setup.InputDelay)
                {
                    Assert.True(_realInputs[slot].TryGetValue(frame, out InputFlags real), $"No real input of slot {slot} at frame {frame}.");
                    input[slot] = real;
                }
            }
            Simulator.Tick(ref reference, input, Data);

            for (int peer = 0; peer < Sessions.Length; peer++)
            {
                if (_confirmedHashes[peer].TryGetValue(reference.Frame, out ulong hash))
                {
                    Assert.True(reference.ComputeHash() == hash, $"Peer {peer}: the confirmed state of frame {reference.Frame} differs from the reference.");
                    compared++;
                }
            }
        }

        Assert.True(compared > lastConfirmed / 4, $"Only {compared} confirmed hashes were compared.");
        return reference;
    }
}
