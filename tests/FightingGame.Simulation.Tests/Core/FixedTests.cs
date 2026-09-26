using System;
using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests.Core;

public class FixedTests
{
    private static Fixed Raw(long raw) => Fixed.FromRaw(raw);

    [Fact]
    public void FromIntUsesSixteenFractionBits()
    {
        Assert.Equal(65536L, Fixed.FromInt(1).Raw);
        Assert.Equal(-3L * 65536, Fixed.FromInt(-3).Raw);
        Assert.Equal(Fixed.FromInt(7), (Fixed)7);
    }

    [Fact]
    public void FromRatioRoundsTowardZero()
    {
        Assert.Equal(98304L, Fixed.FromRatio(3, 2).Raw);   // 1.5
        Assert.Equal(21845L, Fixed.FromRatio(1, 3).Raw);   // 0.33333 -> down
        Assert.Equal(-21845L, Fixed.FromRatio(-1, 3).Raw); // -0.33333 -> up (toward zero)
        Assert.Equal(43690L, Fixed.FromRatio(2, 3).Raw);   // 0.66666 -> down
    }

    [Fact]
    public void AddAndSubtract()
    {
        Assert.Equal(Fixed.FromInt(5), Fixed.FromInt(2) + 3);
        Assert.Equal(Fixed.FromInt(-1), Fixed.FromInt(2) - 3);
        Assert.Equal(Fixed.FromInt(-2), -Fixed.FromInt(2));
    }

    [Fact]
    public void MultiplyExact()
    {
        Assert.Equal(Fixed.FromInt(6), Fixed.FromInt(2) * 3);
        Assert.Equal(Fixed.FromRatio(3, 4), Fixed.FromRatio(3, 2) * Fixed.Half);
        Assert.Equal(Fixed.FromInt(-6), Fixed.FromInt(-2) * 3);
    }

    [Fact]
    public void MultiplyRoundsTowardNegativeInfinity()
    {
        // Epsilon * 0.5 = 0.5 raw units.
        Assert.Equal(0L, (Fixed.Epsilon * Fixed.Half).Raw);
        Assert.Equal(-1L, (-Fixed.Epsilon * Fixed.Half).Raw);
        // 3 raw * 0.5 = 1.5 raw units.
        Assert.Equal(1L, (Raw(3) * Fixed.Half).Raw);
        Assert.Equal(-2L, (Raw(-3) * Fixed.Half).Raw);
    }

    [Fact]
    public void MultiplyHasNoIntermediateOverflow()
    {
        // The raw product needs more than 64 bits, but the result fits.
        Fixed big = Fixed.FromInt(1 << 30);
        Assert.Equal(Fixed.FromInt(1 << 29), big * Fixed.Half);
        Assert.Equal(-Fixed.FromInt(1 << 29), big * -Fixed.Half);
        Assert.Equal(Fixed.FromRatio(100_000L * 100_000L, 1), Fixed.FromInt(100_000) * Fixed.FromInt(100_000));
    }

    [Fact]
    public void DivideRoundsTowardZero()
    {
        Assert.Equal(Fixed.FromRatio(1, 3), Fixed.One / 3);
        Assert.Equal(Fixed.FromRatio(-1, 3), -Fixed.One / 3);
        Assert.Equal(Fixed.FromRatio(-1, 3), Fixed.One / -3);
        Assert.Equal(Fixed.FromInt(4), Fixed.FromInt(2) / Fixed.Half);
    }

    [Fact]
    public void DivideHasNoIntermediateOverflow()
    {
        Fixed big = Fixed.FromRatio(1L << 40, 1);
        Assert.Equal(Fixed.FromRatio(1L << 39, 1), big / 2);
    }

    [Fact]
    public void DivideByZeroThrows()
    {
        Assert.Throws<DivideByZeroException>(() => Fixed.One / Fixed.Zero);
    }

    [Fact]
    public void RemainderHasSignOfDividend()
    {
        Assert.Equal(Fixed.FromInt(1), Fixed.FromInt(7) % 3);
        Assert.Equal(Fixed.FromInt(-1), Fixed.FromInt(-7) % 3);
    }

    [Fact]
    public void Comparison()
    {
        Assert.True(Fixed.FromInt(1) < Fixed.FromInt(2));
        Assert.True(Fixed.FromInt(-1) < Fixed.Zero);
        Assert.True(Fixed.Half <= Fixed.FromRatio(1, 2));
        Assert.True(Fixed.One > Fixed.Half);
        Assert.Equal(-1, Fixed.Zero.CompareTo(Fixed.One));
    }

    [Fact]
    public void AbsMinMaxClampSign()
    {
        Assert.Equal(Fixed.FromInt(3), Fixed.Abs(-3));
        Assert.Equal(Fixed.FromInt(3), Fixed.Abs(3));
        Assert.Equal(Fixed.FromInt(-2), Fixed.Min(-2, 5));
        Assert.Equal(Fixed.FromInt(5), Fixed.Max(-2, 5));
        Assert.Equal(Fixed.FromInt(10), Fixed.Clamp(15, 0, 10));
        Assert.Equal(Fixed.FromInt(0), Fixed.Clamp(-5, 0, 10));
        Assert.Equal(-1, Fixed.Sign(-Fixed.Epsilon));
        Assert.Equal(0, Fixed.Sign(Fixed.Zero));
        Assert.Equal(1, Fixed.Sign(Fixed.Epsilon));
    }

    [Theory]
    // value (as ratio num/den), floor, ceil, round
    [InlineData(5, 2, 2, 3, 3)]       // 2.5
    [InlineData(-5, 2, -3, -2, -2)]   // -2.5: round goes toward +infinity
    [InlineData(7, 4, 1, 2, 2)]       // 1.75
    [InlineData(-7, 4, -2, -1, -2)]   // -1.75
    [InlineData(5, 4, 1, 2, 1)]       // 1.25
    [InlineData(-5, 4, -2, -1, -1)]   // -1.25
    [InlineData(3, 1, 3, 3, 3)]
    [InlineData(-3, 1, -3, -3, -3)]
    public void FloorCeilRound(long num, long den, int floor, int ceil, int round)
    {
        Fixed value = Fixed.FromRatio(num, den);
        Assert.Equal(Fixed.FromInt(floor), Fixed.Floor(value));
        Assert.Equal(Fixed.FromInt(ceil), Fixed.Ceil(value));
        Assert.Equal(Fixed.FromInt(round), Fixed.Round(value));
        Assert.Equal(floor, value.FloorToInt());
        Assert.Equal(ceil, value.CeilToInt());
        Assert.Equal(round, value.RoundToInt());
    }

    [Fact]
    public void ToStringIsExactDecimal()
    {
        Assert.Equal("1.5", Fixed.FromRatio(3, 2).ToString());
        Assert.Equal("-0.25", Fixed.FromRatio(-1, 4).ToString());
        Assert.Equal("0.0000152587890625", Fixed.Epsilon.ToString());
    }
}
