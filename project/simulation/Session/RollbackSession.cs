using System;
using System.Collections.Generic;
using FightingGame.Core;
using FightingGame.Networking;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>Debug counters of a <see cref="RollbackSession"/>.</summary>
public sealed class RollbackSessionStats
{
    /// <summary>The number of rollbacks (a confirmed input differed from the predicted input).</summary>
    public int Rollbacks { get; internal set; }

    /// <summary>The most frames run again in one rollback.</summary>
    public int LongestRollback { get; internal set; }

    /// <summary>The total number of frames run again in rollbacks.</summary>
    public int FramesRunAgain { get; internal set; }

    /// <summary>The number of ticks that waited because the session reached the max prediction.</summary>
    public int PredictionWaits { get; internal set; }

    /// <summary>The number of received messages that were not valid (ignored).</summary>
    public int InvalidMessages { get; internal set; }
}

/// <summary>
/// One peer of an online match with rollback. The local players' inputs are used after the input delay; the inputs of
/// remote players are predicted until they arrive, and a wrong prediction is corrected with a rollback.
/// </summary>
/// <remarks>
/// Each <see cref="AdvanceFrame"/>:
/// <list type="number">
/// <item>Receive the messages: store the new confirmed inputs of the remote slots, and the acknowledgments.</item>
/// <item>If a new confirmed input differs from the input that a frame used: load the snapshot of the first wrong frame
/// and run again to the current frame (rollback). The frames after the correction predict with the new last input.</item>
/// <item>Wait (return false) if the current frame is <see cref="RollbackSessionSetup.MaxPrediction"/> frames past the
/// last frame with all inputs confirmed.</item>
/// <item>Store the local inputs for frame current + <see cref="RollbackSessionSetup.InputDelay"/>.</item>
/// <item>Send the input message to each peer (also when the session waits, so the acknowledgments and the inputs keep flowing).</item>
/// <item>Run the tick with the confirmed or predicted inputs, and save the snapshot.</item>
/// </list>
/// Prediction: a missing input of a slot is the last confirmed input of that slot (players often hold the same buttons).
/// Frames 0 to InputDelay - 1 have no local input, so they use empty inputs for all slots (confirmed at the start).
/// </remarks>
public sealed class RollbackSession : IMatchSession
{
    /// <summary>
    /// The number of frames of confirmed inputs that each slot keeps. It must hold the frames that a rollback can
    /// run again, and the frames that a remote peer can be ahead (2 x (max prediction + input delay) + 2 at most).
    /// </summary>
    private const int InputCapacity = 64;

    private const int NoFrame = int.MaxValue;

    private readonly RollbackSessionSetup _setup;
    private readonly INetworkTransport _transport;
    private readonly PeerId _localPeer;

    /// <summary>The other peers, and for each peer, the mask of the slots that it owns.</summary>
    private readonly PeerId[] _remotePeers;
    private readonly byte[] _remotePeerSlotMasks;
    private readonly byte _localSlotMask;

    /// <summary>For each remote peer: the last frame of local inputs that the peer has acknowledged.</summary>
    private readonly int[] _remoteAckFrames;

    /// <summary>The confirmed inputs of each slot (index frame % <see cref="InputCapacity"/>), and the last confirmed frame.</summary>
    private readonly InputFlags[][] _confirmedInputs;
    private readonly int[] _lastConfirmedFrames;

    /// <summary>The inputs (confirmed or predicted) that each frame used. A new confirmed input is compared with them.</summary>
    private readonly InputHistory _usedInputs;
    private readonly SnapshotBuffer _snapshots;

    private readonly InputMessage _receivedMessage = new();
    private readonly InputMessage _sentMessage = new();
    private readonly byte[] _sendBuffer = new byte[InputMessage.MaxSize];

    private WorldData _world;
    private FrameInput _nextLocalInput;

    /// <summary>The first frame that used a wrong prediction, or <see cref="NoFrame"/>.</summary>
    private int _firstWrongFrame = NoFrame;

    public RollbackSession(GameData data, RollbackSessionSetup setup, INetworkTransport transport)
    {
        _localPeer = transport.LocalPeer;
        setup.Validate(_localPeer);
        Data = data;
        _setup = setup;
        _transport = transport;

        var remotePeers = new List<PeerId>();
        var remoteMasks = new List<byte>();
        for (int slot = 0; slot < setup.PlayerCount; slot++)
        {
            PeerId owner = setup.SlotOwners[slot];
            if (owner == _localPeer)
            {
                _localSlotMask |= (byte)(1 << slot);
                continue;
            }
            int index = remotePeers.IndexOf(owner);
            if (index < 0)
            {
                remotePeers.Add(owner);
                remoteMasks.Add(0);
                index = remotePeers.Count - 1;
            }
            remoteMasks[index] |= (byte)(1 << slot);
        }
        _remotePeers = remotePeers.ToArray();
        _remotePeerSlotMasks = remoteMasks.ToArray();

        // Frames 0 to InputDelay - 1 have empty inputs for all slots. All peers know this, so it is confirmed and acknowledged.
        int lastStartFrame = setup.InputDelay - 1;
        _remoteAckFrames = new int[_remotePeers.Length];
        Array.Fill(_remoteAckFrames, lastStartFrame);
        _confirmedInputs = new InputFlags[setup.PlayerCount][];
        _lastConfirmedFrames = new int[setup.PlayerCount];
        for (int slot = 0; slot < setup.PlayerCount; slot++)
        {
            _confirmedInputs[slot] = new InputFlags[InputCapacity];
            _lastConfirmedFrames[slot] = lastStartFrame;
        }

        _usedInputs = new InputHistory(setup.MaxPrediction + 2);
        _snapshots = new SnapshotBuffer(setup.MaxPrediction + 2);
        _world = WorldData.Create(data, setup.PlayerCount, setup.Seed);
        _snapshots.Save(_world);

        _transport.MessageReceived += OnMessageReceived;
    }

    public GameData Data { get; }

    public ref readonly WorldData World => ref _world;

    public int PlayerCount => _setup.PlayerCount;

    public PeerId LocalPeer => _localPeer;

    public RollbackSessionStats Stats { get; } = new();

    /// <summary>The last frame for which the inputs of all slots are confirmed. -1 = none.</summary>
    public int ConfirmedInputFrame
    {
        get
        {
            int frame = int.MaxValue;
            foreach (int last in _lastConfirmedFrames)
            {
                frame = Math.Min(frame, last);
            }
            return frame;
        }
    }

    /// <summary>
    /// The newest frame whose state depends only on confirmed inputs. It never changes in a rollback, so all peers
    /// have the same state at this frame.
    /// </summary>
    public int ConfirmedFrame => Math.Min(ConfirmedInputFrame + 1, _world.Frame);

    /// <summary>The hash of the state at <see cref="ConfirmedFrame"/>.</summary>
    public ulong ConfirmedFrameHash => _snapshots.HashOf(ConfirmedFrame);

    public bool IsLocalSlot(int slot) => slot >= 0 && slot < PlayerCount && (_localSlotMask & (1 << slot)) != 0;

    public void SetLocalInput(int slot, InputFlags input)
    {
        if (!IsLocalSlot(slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} is not a local slot of {_localPeer}.");
        }
        _nextLocalInput[slot] = input;
    }

    public bool AdvanceFrame()
    {
        _transport.Poll();
        RollBackIfNeeded();

        int frame = _world.Frame;
        if (frame - ConfirmedInputFrame > _setup.MaxPrediction)
        {
            Stats.PredictionWaits++;
            _nextLocalInput = default;
            SendInputs();
            return false;
        }

        StoreLocalInputs(frame + _setup.InputDelay);
        _nextLocalInput = default;
        SendInputs();

        RunFrame(ref _world);
        return true;
    }

    /// <summary>Runs one frame with the confirmed or predicted inputs, stores the used inputs, and saves the snapshot.</summary>
    private void RunFrame(ref WorldData world)
    {
        FrameInput input = InputsOf(world.Frame);
        _usedInputs.Set(world.Frame, input);
        Simulator.Tick(ref world, input, Data);
        _snapshots.Save(world);
    }

    private void RollBackIfNeeded()
    {
        int currentFrame = _world.Frame;
        if (_firstWrongFrame >= currentFrame)
        {
            _firstWrongFrame = NoFrame;
            return;
        }

        int framesAgain = currentFrame - _firstWrongFrame;
        WorldData world = _snapshots.Get(_firstWrongFrame);
        while (world.Frame < currentFrame)
        {
            RunFrame(ref world);
        }
        _world = world;
        _firstWrongFrame = NoFrame;

        Stats.Rollbacks++;
        Stats.FramesRunAgain += framesAgain;
        Stats.LongestRollback = Math.Max(Stats.LongestRollback, framesAgain);
    }

    /// <summary>The inputs of all slots for a frame: the confirmed input, or the prediction (the last confirmed input of the slot).</summary>
    private FrameInput InputsOf(int frame)
    {
        FrameInput input = default;
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            int last = _lastConfirmedFrames[slot];
            int known = frame <= last ? frame : last;
            input[slot] = known >= 0 ? _confirmedInputs[slot][known % InputCapacity] : InputFlags.None;
        }
        return input;
    }

    private void StoreLocalInputs(int frame)
    {
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            if (IsLocalSlot(slot))
            {
                _confirmedInputs[slot][frame % InputCapacity] = _nextLocalInput[slot];
                _lastConfirmedFrames[slot] = frame;
            }
        }
    }

    /// <summary>
    /// Sends each remote peer the local inputs that it has not acknowledged (the oldest first, at most
    /// <see cref="InputMessage.MaxFrames"/>), and the acknowledgment of its inputs.
    /// </summary>
    private void SendInputs()
    {
        int lastLocalFrame = LastConfirmedFrameOf(_localSlotMask);
        for (int peer = 0; peer < _remotePeers.Length; peer++)
        {
            int startFrame = _remoteAckFrames[peer] + 1;
            InputMessage message = _sentMessage;
            message.SenderFrame = _world.Frame;
            message.FrameAdvantage = 0;
            message.AckFrame = LastConfirmedFrameOf(_remotePeerSlotMasks[peer]);
            message.StartFrame = startFrame;
            message.SlotMask = _localSlotMask;
            message.FrameCount = Math.Clamp(lastLocalFrame - startFrame + 1, 0, InputMessage.MaxFrames);
            for (int offset = 0; offset < message.FrameCount; offset++)
            {
                for (int slot = 0; slot < PlayerCount; slot++)
                {
                    if (IsLocalSlot(slot))
                    {
                        message.SetInput(offset, slot, _confirmedInputs[slot][(startFrame + offset) % InputCapacity]);
                    }
                }
            }

            var writer = new MessageWriter(_sendBuffer);
            message.Write(ref writer);
            _transport.Send(_remotePeers[peer], writer.Written, DeliveryMode.Unreliable);
        }
    }

    private void OnMessageReceived(PeerId from, ReadOnlySpan<byte> data)
    {
        int peer = Array.IndexOf(_remotePeers, from);
        if (peer < 0)
        {
            Stats.InvalidMessages++;
            return;
        }

        try
        {
            switch (MessageTypes.Peek(data))
            {
                case MessageType.Input:
                    var reader = new MessageReader(data);
                    _receivedMessage.Read(ref reader);
                    ReceiveInputs(peer, _receivedMessage);
                    break;
                case MessageType.Hash:
                    // Desync detection is not used yet.
                    break;
                default:
                    Stats.InvalidMessages++;
                    break;
            }
        }
        catch (MessageFormatException)
        {
            Stats.InvalidMessages++;
        }
    }

    private void ReceiveInputs(int peer, InputMessage message)
    {
        byte peerSlots = _remotePeerSlotMasks[peer];
        if (message.SlotMask != peerSlots)
        {
            Stats.InvalidMessages++;
            return;
        }

        // The acknowledgment only grows, and never past the local inputs that exist.
        int ack = Math.Min(message.AckFrame, LastConfirmedFrameOf(_localSlotMask));
        _remoteAckFrames[peer] = Math.Max(_remoteAckFrames[peer], ack);

        // Inputs of frames past this limit would overwrite inputs that a rollback can still need. The sender sends them again.
        int frameLimit = Math.Max(0, _world.Frame - _setup.MaxPrediction - 1) + InputCapacity;
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            if ((peerSlots & (1 << slot)) == 0)
            {
                continue;
            }

            for (int offset = 0; offset < message.FrameCount; offset++)
            {
                int frame = message.StartFrame + offset;
                if (frame <= _lastConfirmedFrames[slot])
                {
                    continue;   // Already confirmed (the message repeats inputs).
                }
                if (frame != _lastConfirmedFrames[slot] + 1 || frame >= frameLimit)
                {
                    break;      // Confirmed inputs must have no gaps.
                }

                InputFlags input = message.GetInput(offset, slot);
                _confirmedInputs[slot][frame % InputCapacity] = input;
                _lastConfirmedFrames[slot] = frame;

                if (frame < _world.Frame && _usedInputs.Get(frame)[slot] != input)
                {
                    _firstWrongFrame = Math.Min(_firstWrongFrame, frame);
                }
            }
        }
    }

    /// <summary>The last frame for which the inputs of all slots in the mask are confirmed.</summary>
    private int LastConfirmedFrameOf(byte slotMask)
    {
        int frame = int.MaxValue;
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            if ((slotMask & (1 << slot)) != 0)
            {
                frame = Math.Min(frame, _lastConfirmedFrames[slot]);
            }
        }
        return frame;
    }
}
