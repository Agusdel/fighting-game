using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests.Core;

public class StateHasherTests
{
    // Reference values: published FNV-1a 64-bit test vectors, and an independent Python implementation.

    [Fact]
    public void EmptyHashIsOffsetBasis()
    {
        Assert.Equal(0xcbf29ce484222325UL, new StateHasher().Value);
    }

    [Fact]
    public void MatchesFnv1aTestVectors()
    {
        Assert.Equal(0xaf63dc4c8601ec8cUL, HashBytes("a"));
        Assert.Equal(0x85944171f73967e8UL, HashBytes("foobar"));
    }

    [Fact]
    public void IntIsLittleEndian()
    {
        var hasher = new StateHasher();
        hasher.Add(1);
        Assert.Equal(0xad2aca7747985764UL, hasher.Value);
    }

    [Fact]
    public void FixedHashesRawLong()
    {
        var hasher = new StateHasher();
        hasher.Add(Fixed.One);
        Assert.Equal(0xcc3193ff3a28738cUL, hasher.Value);
    }

    [Fact]
    public void OrderChangesHash()
    {
        var a = new StateHasher();
        a.Add(1);
        a.Add(2);
        var b = new StateHasher();
        b.Add(2);
        b.Add(1);
        Assert.NotEqual(a.Value, b.Value);
    }

    [Fact]
    public void VectorAndBoxHashAllComponents()
    {
        var box = new StateHasher();
        box.Add(new FixedAABB(new FixedVector2(1, 2), new FixedVector2(3, 4)));

        var parts = new StateHasher();
        parts.Add((Fixed)1);
        parts.Add((Fixed)2);
        parts.Add((Fixed)3);
        parts.Add((Fixed)4);

        Assert.Equal(parts.Value, box.Value);
    }

    private static ulong HashBytes(string text)
    {
        var hasher = new StateHasher();
        foreach (char c in text)
        {
            hasher.Add((byte)c);
        }
        return hasher.Value;
    }
}
