using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests.Core;

public class FixedRngTests
{
    // Reference values: SplitMix64 seeding + xorshift64*, computed with an independent Python implementation.

    [Fact]
    public void SeedUsesSplitMix64()
    {
        Assert.Equal(0xe220a8397b1dcdafUL, new FixedRng(0).State);
        Assert.Equal(0x22118258a9d111a0UL, new FixedRng(12345).State);
    }

    [Fact]
    public void SequenceMatchesReference()
    {
        var rng = new FixedRng(12345);
        Assert.Equal(0x47edfd1cd809b6dcUL, rng.NextULong());
        Assert.Equal(0x34d004209d31c6baUL, rng.NextULong());
        Assert.Equal(0x38b855ac9296d1e9UL, rng.NextULong());
    }

    [Fact]
    public void CopyContinuesTheSameSequence()
    {
        var rng = new FixedRng(7);
        rng.NextULong();
        FixedRng copy = rng;
        Assert.Equal(rng.NextULong(), copy.NextULong());
    }

    [Fact]
    public void NextIntStaysInRange()
    {
        var rng = new FixedRng(1);
        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.NextInt(-3, 4);
            Assert.InRange(value, -3, 3);
        }
    }

    [Fact]
    public void NextFixedIsInUnitRange()
    {
        var rng = new FixedRng(1);
        for (int i = 0; i < 10_000; i++)
        {
            Fixed value = rng.NextFixed();
            Assert.True(value >= Fixed.Zero && value < Fixed.One);
        }
    }

    [Fact]
    public void HashChangesWhenStateAdvances()
    {
        var rng = new FixedRng(1);
        var before = new StateHasher();
        rng.Hash(ref before);
        rng.NextULong();
        var after = new StateHasher();
        rng.Hash(ref after);
        Assert.NotEqual(before.Value, after.Value);
    }
}
