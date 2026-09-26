using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests.Core;

public class FixedVector2Tests
{
    private static FixedVector2 V(int x, int y) => new(x, y);

    [Fact]
    public void Arithmetic()
    {
        Assert.Equal(V(4, 6), V(1, 2) + V(3, 4));
        Assert.Equal(V(-2, -2), V(1, 2) - V(3, 4));
        Assert.Equal(V(-1, -2), -V(1, 2));
        Assert.Equal(V(2, 4), V(1, 2) * 2);
        Assert.Equal(V(2, 4), (Fixed)2 * V(1, 2));
        Assert.Equal(V(1, 2), V(2, 4) / 2);
    }

    [Fact]
    public void WithComponent()
    {
        Assert.Equal(V(9, 2), V(1, 2).WithX(9));
        Assert.Equal(V(1, 9), V(1, 2).WithY(9));
    }

    [Fact]
    public void Equality()
    {
        Assert.True(V(1, 2) == V(1, 2));
        Assert.True(V(1, 2) != V(2, 1));
        Assert.Equal(FixedVector2.Zero, V(0, 0));
    }
}
