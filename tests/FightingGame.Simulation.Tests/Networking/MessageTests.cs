using System;
using FightingGame.Networking;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Networking;

public class MessageTests
{
    [Fact]
    public void WriterUsesLittleEndian()
    {
        Span<byte> buffer = stackalloc byte[15];
        var writer = new MessageWriter(buffer);
        writer.WriteByte(0xAB);
        writer.WriteUShort(0x0102);
        writer.WriteInt(0x03040506);
        writer.WriteULong(0x0708090A0B0C0D0E);

        Assert.Equal(15, writer.Length);
        Assert.Equal(new byte[] { 0xAB, 0x02, 0x01, 0x06, 0x05, 0x04, 0x03, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07 },
            writer.Written.ToArray());
    }

    [Fact]
    public void ReaderReadsWhatTheWriterWrote()
    {
        Span<byte> buffer = stackalloc byte[15];
        var writer = new MessageWriter(buffer);
        writer.WriteByte(200);
        writer.WriteUShort(65000);
        writer.WriteInt(-5);
        writer.WriteULong(ulong.MaxValue - 1);

        var reader = new MessageReader(writer.Written);
        Assert.Equal(200, reader.ReadByte());
        Assert.Equal(65000, reader.ReadUShort());
        Assert.Equal(-5, reader.ReadInt());
        Assert.Equal(ulong.MaxValue - 1, reader.ReadULong());
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void WriterRejectsAFullBuffer()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var writer = new MessageWriter(new byte[3]);
            writer.WriteInt(1);
        });
    }

    [Fact]
    public void ReaderRejectsAShortMessage()
    {
        Assert.Throws<MessageFormatException>(() =>
        {
            var reader = new MessageReader(new byte[3]);
            reader.ReadInt();
        });
    }

    private static InputMessage CreateInputMessage()
    {
        var message = new InputMessage
        {
            SenderFrame = 120,
            FrameAdvantage = -2,
            AckFrame = 110,
            StartFrame = 100,
            FrameCount = InputMessage.MaxFrames,
            SlotMask = 0b1010,   // Slots 1 and 3.
        };
        for (int frame = 0; frame < message.FrameCount; frame++)
        {
            message.SetInput(frame, 1, (InputFlags)frame);
            message.SetInput(frame, 3, InputFlags.Jump | (InputFlags)(1 << 15));
        }
        return message;
    }

    [Fact]
    public void InputMessageRoundTrip()
    {
        InputMessage sent = CreateInputMessage();
        var buffer = new byte[InputMessage.MaxSize];
        var writer = new MessageWriter(buffer);
        sent.Write(ref writer);

        Assert.Equal(MessageType.Input, MessageTypes.Peek(writer.Written));
        // Header (19 bytes) + 32 frames x 2 slots x 2 bytes.
        Assert.Equal(19 + 32 * 2 * 2, writer.Length);

        var received = new InputMessage();
        var reader = new MessageReader(writer.Written);
        received.Read(ref reader);

        Assert.Equal(0, reader.Remaining);
        Assert.Equal(120, received.SenderFrame);
        Assert.Equal(-2, received.FrameAdvantage);
        Assert.Equal(110, received.AckFrame);
        Assert.Equal(100, received.StartFrame);
        Assert.Equal(InputMessage.MaxFrames, received.FrameCount);
        Assert.Equal(0b1010, received.SlotMask);
        for (int frame = 0; frame < received.FrameCount; frame++)
        {
            Assert.Equal(sent.GetInput(frame, 1), received.GetInput(frame, 1));
            Assert.Equal(sent.GetInput(frame, 3), received.GetInput(frame, 3));
        }
    }

    [Fact]
    public void LargestInputMessageFitsMaxSize()
    {
        var message = new InputMessage { FrameCount = InputMessage.MaxFrames, SlotMask = 0b1111 };
        var writer = new MessageWriter(new byte[InputMessage.MaxSize]);
        message.Write(ref writer);
        Assert.Equal(InputMessage.MaxSize, writer.Length);
    }

    [Fact]
    public void InputMessageRejectsInvalidValues()
    {
        var message = new InputMessage { FrameCount = 2, SlotMask = 0b0001 };
        Assert.Throws<ArgumentOutOfRangeException>(() => message.FrameCount = InputMessage.MaxFrames + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => message.SlotMask = 0b10000);
        Assert.Throws<ArgumentOutOfRangeException>(() => message.GetInput(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => message.SetInput(0, 1, InputFlags.Jump));
    }

    [Theory]
    [InlineData(17, 33)]           // Too many frames.
    [InlineData(18, 0b10000)]      // Slot 4 does not exist.
    public void InputMessageReadRejectsInvalidData(int index, byte value)
    {
        InputMessage sent = CreateInputMessage();
        var buffer = new byte[InputMessage.MaxSize];
        var writer = new MessageWriter(buffer);
        sent.Write(ref writer);
        byte[] data = writer.Written.ToArray();
        data[index] = value;

        Assert.Throws<MessageFormatException>(() =>
        {
            var reader = new MessageReader(data);
            new InputMessage().Read(ref reader);
        });
    }

    [Fact]
    public void InputMessageReadRejectsAShortMessage()
    {
        InputMessage sent = CreateInputMessage();
        var buffer = new byte[InputMessage.MaxSize];
        var writer = new MessageWriter(buffer);
        sent.Write(ref writer);
        byte[] data = writer.Written[..^1].ToArray();

        Assert.Throws<MessageFormatException>(() =>
        {
            var reader = new MessageReader(data);
            new InputMessage().Read(ref reader);
        });
    }

    [Fact]
    public void HashMessageRoundTrip()
    {
        var writer = new MessageWriter(new byte[HashMessage.Size]);
        new HashMessage { Frame = 90, Hash = 0x1234567890ABCDEF }.Write(ref writer);
        Assert.Equal(HashMessage.Size, writer.Length);
        Assert.Equal(MessageType.Hash, MessageTypes.Peek(writer.Written));

        var reader = new MessageReader(writer.Written);
        HashMessage received = HashMessage.Read(ref reader);
        Assert.Equal(90, received.Frame);
        Assert.Equal(0x1234567890ABCDEFUL, received.Hash);
    }

    [Fact]
    public void ReadRejectsTheWrongMessageType()
    {
        var writer = new MessageWriter(new byte[HashMessage.Size]);
        new HashMessage { Frame = 1, Hash = 2 }.Write(ref writer);
        byte[] data = writer.Written.ToArray();

        Assert.Throws<MessageFormatException>(() =>
        {
            var reader = new MessageReader(data);
            new InputMessage().Read(ref reader);
        });
        Assert.Throws<MessageFormatException>(() => MessageTypes.Peek(ReadOnlySpan<byte>.Empty));
    }
}
