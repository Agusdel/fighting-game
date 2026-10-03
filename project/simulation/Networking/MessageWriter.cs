using System;
using System.Buffers.Binary;

namespace FightingGame.Networking;

/// <summary>
/// Writes message values into a buffer, in little-endian byte order (the same on all machines).
/// It allocates nothing. Writing past the end of the buffer throws <see cref="InvalidOperationException"/>.
/// </summary>
public ref struct MessageWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public MessageWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>The number of bytes written.</summary>
    public readonly int Length => _position;

    /// <summary>The bytes written.</summary>
    public readonly ReadOnlySpan<byte> Written => _buffer[.._position];

    public void WriteByte(byte value) => Next(1)[0] = value;

    public void WriteUShort(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Next(2), value);

    public void WriteInt(int value) => BinaryPrimitives.WriteInt32LittleEndian(Next(4), value);

    public void WriteULong(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(Next(8), value);

    private Span<byte> Next(int size)
    {
        if (_position + size > _buffer.Length)
        {
            throw new InvalidOperationException($"The message buffer is too small ({_buffer.Length} bytes).");
        }
        Span<byte> span = _buffer.Slice(_position, size);
        _position += size;
        return span;
    }
}
