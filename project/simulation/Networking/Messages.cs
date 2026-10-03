using System;
using FightingGame.Core;
using FightingGame.Simulation;

namespace FightingGame.Networking;

/// <summary>The first byte of each message.</summary>
public enum MessageType : byte
{
    Input = 1,
    Hash = 2,
}

public static class MessageTypes
{
    /// <summary>The type of a received message, read from its first byte.</summary>
    public static MessageType Peek(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new MessageFormatException("The message is empty.");
        }
        return (MessageType)data[0];
    }

    internal static void Expect(ref MessageReader reader, MessageType expected)
    {
        var type = (MessageType)reader.ReadByte();
        if (type != expected)
        {
            throw new MessageFormatException($"Expected a {expected} message, but the message type is {type}.");
        }
    }
}

/// <summary>
/// The inputs of a peer's local player slots for a range of frames, and the values for acknowledgment and time sync.
/// Sent unreliably on each frame. The range repeats all inputs that the receiver has not acknowledged,
/// so a lost message does no harm when a later message arrives.
/// </summary>
/// <remarks>
/// One instance is reused for many messages (no allocation in the frame loop): <see cref="Read"/> replaces all values.
/// Layout: type, sender frame, frame advantage, ack frame, start frame (int32 each), frame count, slot mask (byte),
/// then for each frame, the inputs (uint16) of the slots in the mask, from the lowest slot.
/// </remarks>
public sealed class InputMessage
{
    /// <summary>The maximum number of frames in one message.</summary>
    public const int MaxFrames = 32;

    /// <summary>The size of the largest message (all frames, all slots).</summary>
    public const int MaxSize = 1 + 4 * 4 + 1 + 1 + MaxFrames * GameConstants.MaxPlayers * 2;

    private readonly InputFlags[] _inputs = new InputFlags[MaxFrames * GameConstants.MaxPlayers];
    private int _frameCount;
    private byte _slotMask;

    /// <summary>The current frame of the sender when it sent the message (for time sync).</summary>
    public int SenderFrame { get; set; }

    /// <summary>The sender's frame advantage over the receiver, as the sender measured it (for time sync).</summary>
    public int FrameAdvantage { get; set; }

    /// <summary>The last frame for which the sender has all inputs of the receiver's slots. -1 = none.</summary>
    public int AckFrame { get; set; } = -1;

    /// <summary>The frame of the first inputs in the message.</summary>
    public int StartFrame { get; set; }

    /// <summary>The number of frames in the message (0 to <see cref="MaxFrames"/>).</summary>
    public int FrameCount
    {
        get => _frameCount;
        set
        {
            if (value < 0 || value > MaxFrames)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"The frame count must be 0 to {MaxFrames}.");
            }
            _frameCount = value;
        }
    }

    /// <summary>Bit i = the message has the inputs of player slot i (the sender's local slots).</summary>
    public byte SlotMask
    {
        get => _slotMask;
        set
        {
            if (value >> GameConstants.MaxPlayers != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"The slot mask has a slot that is not below {GameConstants.MaxPlayers}.");
            }
            _slotMask = value;
        }
    }

    public bool HasSlot(int slot) => slot >= 0 && slot < GameConstants.MaxPlayers && (_slotMask & (1 << slot)) != 0;

    /// <summary>The input of <paramref name="slot"/> at frame <see cref="StartFrame"/> + <paramref name="frameOffset"/>.</summary>
    public InputFlags GetInput(int frameOffset, int slot) => _inputs[IndexOf(frameOffset, slot)];

    public void SetInput(int frameOffset, int slot, InputFlags input) => _inputs[IndexOf(frameOffset, slot)] = input;

    public void Write(ref MessageWriter writer)
    {
        writer.WriteByte((byte)MessageType.Input);
        writer.WriteInt(SenderFrame);
        writer.WriteInt(FrameAdvantage);
        writer.WriteInt(AckFrame);
        writer.WriteInt(StartFrame);
        writer.WriteByte((byte)_frameCount);
        writer.WriteByte(_slotMask);
        for (int frame = 0; frame < _frameCount; frame++)
        {
            for (int slot = 0; slot < GameConstants.MaxPlayers; slot++)
            {
                if (HasSlot(slot))
                {
                    writer.WriteUShort((ushort)GetInput(frame, slot));
                }
            }
        }
    }

    /// <summary>Replaces all values with the values of the message. Throws <see cref="MessageFormatException"/> if it is not valid.</summary>
    public void Read(ref MessageReader reader)
    {
        MessageTypes.Expect(ref reader, MessageType.Input);
        SenderFrame = reader.ReadInt();
        FrameAdvantage = reader.ReadInt();
        AckFrame = reader.ReadInt();
        StartFrame = reader.ReadInt();
        int frameCount = reader.ReadByte();
        byte slotMask = reader.ReadByte();
        if (frameCount > MaxFrames)
        {
            throw new MessageFormatException($"The input message has {frameCount} frames (max {MaxFrames}).");
        }
        if (slotMask >> GameConstants.MaxPlayers != 0)
        {
            throw new MessageFormatException($"The input message has a slot mask that is not valid ({slotMask}).");
        }
        if (SenderFrame < 0 || StartFrame < 0 || AckFrame < -1)
        {
            throw new MessageFormatException("The input message has a negative frame.");
        }

        _frameCount = frameCount;
        _slotMask = slotMask;
        Array.Clear(_inputs);
        for (int frame = 0; frame < _frameCount; frame++)
        {
            for (int slot = 0; slot < GameConstants.MaxPlayers; slot++)
            {
                if (HasSlot(slot))
                {
                    SetInput(frame, slot, (InputFlags)reader.ReadUShort());
                }
            }
        }
    }

    private int IndexOf(int frameOffset, int slot)
    {
        if (frameOffset < 0 || frameOffset >= _frameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameOffset), $"Frame offset {frameOffset} is not in the message ({_frameCount} frames).");
        }
        if (!HasSlot(slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} is not in the message.");
        }
        return frameOffset * GameConstants.MaxPlayers + slot;
    }
}

/// <summary>The hash of a confirmed frame of the sender, for desync detection.</summary>
public struct HashMessage
{
    /// <summary>The size of the message.</summary>
    public const int Size = 1 + 4 + 8;

    public int Frame;
    public ulong Hash;

    public readonly void Write(ref MessageWriter writer)
    {
        writer.WriteByte((byte)MessageType.Hash);
        writer.WriteInt(Frame);
        writer.WriteULong(Hash);
    }

    public static HashMessage Read(ref MessageReader reader)
    {
        MessageTypes.Expect(ref reader, MessageType.Hash);
        var message = new HashMessage { Frame = reader.ReadInt(), Hash = reader.ReadULong() };
        if (message.Frame < 0)
        {
            throw new MessageFormatException("The hash message has a negative frame.");
        }
        return message;
    }
}
