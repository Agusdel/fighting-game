namespace FightingGame.Core;

/// <summary>
/// Deterministic pseudo-random number generator (xorshift64*). It is part of the world state:
/// save, restore, and hash it with the rest of the state.
/// </summary>
/// <remarks>
/// This is a mutable struct. Call its methods on the field in the state (for example <c>state.Rng.NextInt(10)</c>),
/// not on a copy, or the state does not advance.
/// </remarks>
public struct FixedRng
{
    private ulong _state;

    /// <summary>Creates a generator. The seed goes through SplitMix64, so every seed (also 0) gives a valid state.</summary>
    public FixedRng(ulong seed)
    {
        ulong z = unchecked(seed + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        _state = z != 0 ? z : 0x9E3779B97F4A7C15UL;
    }

    /// <summary>The internal state. For hashing and tests.</summary>
    public readonly ulong State => _state;

    public ulong NextULong()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return unchecked(_state * 0x2545F4914F6CDD1DUL);
    }

    /// <summary>Returns the high 32 bits of <see cref="NextULong"/> (the best bits of xorshift64*).</summary>
    public uint NextUInt() => (uint)(NextULong() >> 32);

    /// <summary>Returns a value in [0, maxExclusive). <paramref name="maxExclusive"/> must be greater than 0.</summary>
    public int NextInt(int maxExclusive) => (int)(((ulong)NextUInt() * (uint)maxExclusive) >> 32);

    /// <summary>Returns a value in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive) =>
        minInclusive + NextInt(maxExclusive - minInclusive);

    /// <summary>Returns a value in [0, 1).</summary>
    public Fixed NextFixed() => Fixed.FromRaw(NextUInt() >> (32 - Fixed.FractionBits));

    public readonly void Hash(ref StateHasher hasher) => hasher.Add(_state);
}
