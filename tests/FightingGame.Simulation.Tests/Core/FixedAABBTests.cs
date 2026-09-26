using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests.Core;

public class FixedAABBTests
{
    private static FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
        new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

    [Fact]
    public void FromMinSize()
    {
        Assert.Equal(Box(1, 2, 11, 22), FixedAABB.FromMinSize(new(1, 2), new(10, 20)));
    }

    [Fact]
    public void FromCenterSize()
    {
        Assert.Equal(Box(-5, -10, 5, 10), FixedAABB.FromCenterSize(FixedVector2.Zero, new(10, 20)));
        // Odd size: the half size rounds down, and the size stays exact.
        FixedAABB odd = FixedAABB.FromCenterSize(FixedVector2.Zero, new(3, 3));
        Assert.Equal(new FixedVector2(3, 3), odd.Size);
    }

    [Fact]
    public void SizeWidthHeight()
    {
        FixedAABB box = Box(1, 2, 11, 22);
        Assert.Equal((Fixed)10, box.Width);
        Assert.Equal((Fixed)20, box.Height);
        Assert.Equal(new FixedVector2(10, 20), box.Size);
    }

    [Fact]
    public void OverlapsWhenInteriorsIntersect()
    {
        Assert.True(Box(0, 0, 10, 10).Overlaps(Box(5, 5, 15, 15)));
        Assert.True(Box(0, 0, 10, 10).Overlaps(Box(2, 2, 3, 3)));
    }

    [Fact]
    public void TouchingEdgesDoNotOverlap()
    {
        Assert.False(Box(0, 0, 10, 10).Overlaps(Box(10, 0, 20, 10)));
        Assert.False(Box(0, 0, 10, 10).Overlaps(Box(0, 10, 10, 20)));
        Assert.False(Box(0, 0, 10, 10).Overlaps(Box(20, 20, 30, 30)));
    }

    [Fact]
    public void OneEpsilonIntoTheBoxOverlaps()
    {
        FixedAABB floor = Box(0, 10, 100, 20);
        FixedAABB feet = new(new FixedVector2(0, 0), new FixedVector2(10, (Fixed)10 + Fixed.Epsilon));
        Assert.True(floor.Overlaps(feet));
    }

    [Fact]
    public void Translate()
    {
        Assert.Equal(Box(5, 7, 15, 17), Box(0, 0, 10, 10).Translate(new(5, 7)));
    }

    [Fact]
    public void MirrorX()
    {
        // A hitbox in front of a fighter at x = 0, mirrored for the other facing.
        Assert.Equal(Box(-30, -5, -10, 5), Box(10, -5, 30, 5).MirrorX(0));
        Assert.Equal(Box(70, 0, 90, 10), Box(110, 0, 130, 10).MirrorX(100));
    }
}
