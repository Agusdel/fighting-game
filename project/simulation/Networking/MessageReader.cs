using System;
using System.Buffers.Binary;

namespace FightingGame.Networking;

/// <summary>A received message is not valid (too short, or a value out of range).</summary>
public sealed class MessageFormatException : Exception
{
    public MessageFormatException(string message) : base(message)
    {
    }
}

/// <summary>
/// Reads the values that <see cref="MessageWriter"/> wrote. Data from the network can be damaged or false,
/// so reading past the end throws <see cref="MessageFormatException"/>, never a different exception.
/// </summary>
public ref struct MessageReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public MessageReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    /// <summary>The number of bytes not read yet.</summary>
    public readonly int Remaining => _data.Length - _position;

    public byte ReadByte() => Next(1)[0];

    public ushort ReadUShort() => BinaryPrimitives.ReadUInt16LittleEndian(Next(2));

    public int ReadInt() => BinaryPrimitives.ReadInt32LittleEndian(Next(4));

    public ulong ReadULong() => BinaryPrimitives.ReadUInt64LittleEndian(Next(8));

    private ReadOnlySpan<byte> Next(int size)
    {
        if (size > Remaining)
        {
            throw new MessageFormatException($"The message is too short ({_data.Length} bytes).");
        }
        ReadOnlySpan<byte> span = _data.Slice(_position, size);
        _position += size;
        return span;
    }
}
