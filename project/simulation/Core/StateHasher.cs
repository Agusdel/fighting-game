namespace FightingGame.Core;

/// <summary>
/// FNV-1a 64-bit hash of state values. Each value is added field by field, in little-endian byte order,
/// so the result does not depend on struct padding or on the machine byte order.
/// Start with <c>default</c>: <c>var hasher = new StateHasher();</c>
/// </summary>
public struct StateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    // Stored as (hash XOR OffsetBasis), so a default StateHasher starts at the FNV offset basis.
    private ulong _stored;

    /// <summary>The current hash value.</summary>
    public readonly ulong Value => _stored ^ OffsetBasis;

    public void Add(byte value)
    {
        ulong hash = unchecked((Value ^ value) * Prime);
        _stored = hash ^ OffsetBasis;
    }

    public void Add(bool value) => Add(value ? (byte)1 : (byte)0);
    public void Add(sbyte value) => Add(unchecked((byte)value));
    public void Add(short value) => Add(unchecked((ushort)value));
    public void Add(int value) => Add(unchecked((uint)value));
    public void Add(long value) => Add(unchecked((ulong)value));

    public void Add(ushort value)
    {
        Add((byte)value);
        Add((byte)(value >> 8));
    }

    public void Add(uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            Add((byte)(value >> shift));
        }
    }

    public void Add(ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            Add((byte)(value >> shift));
        }
    }

    /// <summary>Adds the length, then each UTF-16 code unit. For static data (names), not for rollback state.</summary>
    public void Add(string value)
    {
        Add(value.Length);
        foreach (char c in value)
        {
            Add((ushort)c);
        }
    }

    public void Add(Fixed value) => Add(value.Raw);

    public void Add(FixedVector2 value)
    {
        Add(value.X);
        Add(value.Y);
    }

    public void Add(FixedAABB value)
    {
        Add(value.Min);
        Add(value.Max);
    }
}
