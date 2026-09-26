using System;

namespace FightingGame.Core;

/// <summary>
/// Deterministic fixed-point number in Q48.16 format (48 integer bits, 16 fraction bits), stored in a <see cref="long"/>.
/// 1 unit = 1 pixel.
/// </summary>
/// <remarks>
/// Rounding rules:
/// <list type="bullet">
/// <item>Multiply: exact 128-bit product, then arithmetic shift right by 16. Rounds toward negative infinity.</item>
/// <item>Divide and <see cref="FromRatio"/>: exact 128-bit dividend, then integer division. Rounds toward zero.</item>
/// <item><see cref="Round"/>: floor(x + 0.5). Halves round toward positive infinity.</item>
/// </list>
/// All arithmetic is unchecked. A result that does not fit in Q48.16 wraps (deterministic).
/// Division by zero throws <see cref="DivideByZeroException"/>.
/// There is no conversion from float. Conversion to float is only in the presentation layer.
/// </remarks>
public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
{
    public const int FractionBits = 16;
    public const long OneRaw = 1L << FractionBits;
    private const long FractionMask = OneRaw - 1;

    public static readonly Fixed Zero = new(0);
    public static readonly Fixed One = new(OneRaw);
    public static readonly Fixed Half = new(OneRaw >> 1);
    public static readonly Fixed Epsilon = new(1);
    public static readonly Fixed MaxValue = new(long.MaxValue);
    public static readonly Fixed MinValue = new(long.MinValue);

    /// <summary>The raw Q48.16 value.</summary>
    public readonly long Raw;

    private Fixed(long raw) => Raw = raw;

    public static Fixed FromRaw(long raw) => new(raw);

    public static Fixed FromInt(int value) => new((long)value << FractionBits);

    /// <summary>Returns numerator / denominator. Rounds toward zero.</summary>
    public static Fixed FromRatio(long numerator, long denominator) =>
        new(unchecked((long)(((Int128)numerator << FractionBits) / denominator)));

    public static implicit operator Fixed(int value) => FromInt(value);

    // Arithmetic

    public static Fixed operator +(Fixed a, Fixed b) => new(unchecked(a.Raw + b.Raw));
    public static Fixed operator -(Fixed a, Fixed b) => new(unchecked(a.Raw - b.Raw));
    public static Fixed operator -(Fixed a) => new(unchecked(-a.Raw));

    public static Fixed operator *(Fixed a, Fixed b)
    {
        // Exact 128-bit product. The shift of (high:low) by FractionBits is arithmetic, so it rounds toward negative infinity.
        long high = Math.BigMul(a.Raw, b.Raw, out long low);
        return new(unchecked((high << (64 - FractionBits)) | (long)((ulong)low >> FractionBits)));
    }

    public static Fixed operator /(Fixed a, Fixed b) =>
        new(unchecked((long)(((Int128)a.Raw << FractionBits) / b.Raw)));

    /// <summary>Remainder. The sign of the result is the sign of <paramref name="a"/> (same as C# integer %).</summary>
    public static Fixed operator %(Fixed a, Fixed b) => new(a.Raw % b.Raw);

    // Comparison

    public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
    public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;
    public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
    public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
    public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
    public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

    // Math

    public static Fixed Abs(Fixed value) => value.Raw < 0 ? new(unchecked(-value.Raw)) : value;
    public static Fixed Min(Fixed a, Fixed b) => a.Raw <= b.Raw ? a : b;
    public static Fixed Max(Fixed a, Fixed b) => a.Raw >= b.Raw ? a : b;
    public static Fixed Clamp(Fixed value, Fixed min, Fixed max) => Min(Max(value, min), max);

    /// <summary>Returns -1, 0, or 1.</summary>
    public static int Sign(Fixed value) => value.Raw > 0 ? 1 : value.Raw < 0 ? -1 : 0;

    public static Fixed Floor(Fixed value) => new(value.Raw & ~FractionMask);
    public static Fixed Ceil(Fixed value) => new(unchecked(value.Raw + FractionMask) & ~FractionMask);
    public static Fixed Round(Fixed value) => Floor(new(unchecked(value.Raw + (OneRaw >> 1))));

    public int FloorToInt() => (int)(Raw >> FractionBits);
    public int CeilToInt() => (int)(Ceil(this).Raw >> FractionBits);
    public int RoundToInt() => (int)(Round(this).Raw >> FractionBits);

    // Object

    public bool Equals(Fixed other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fixed other && Equals(other);
    public override int GetHashCode() => Raw.GetHashCode();
    public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

    /// <summary>Exact decimal text (for logs and debug only).</summary>
    public override string ToString() =>
        ((decimal)Raw / OneRaw).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
