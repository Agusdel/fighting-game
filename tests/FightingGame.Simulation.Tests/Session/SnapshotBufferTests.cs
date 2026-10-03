using System;
using FightingGame.Session;
using FightingGame.Simulation;
using FightingGame.Simulation.Tests.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class SnapshotBufferTests
{
    private static WorldData WorldAtFrame(int frame)
    {
        WorldData world = WorldData.Create(TestStages.CreateGameData(), 2, 3);
        world.Frame = frame;
        return world;
    }

    [Fact]
    public void IsEmptyAtStart()
    {
        var buffer = new SnapshotBuffer(4);
        Assert.False(buffer.Contains(0));
        Assert.Throws<InvalidOperationException>(() => buffer.Get(0));
        Assert.Throws<InvalidOperationException>(() => buffer.HashOf(0));
    }

    [Fact]
    public void SaveStoresTheStateAndItsHash()
    {
        var buffer = new SnapshotBuffer(4);
        WorldData world = WorldAtFrame(5);

        ulong hash = buffer.Save(world);

        Assert.True(buffer.Contains(5));
        Assert.Equal(world.ComputeHash(), hash);
        Assert.Equal(hash, buffer.HashOf(5));
        Assert.Equal(hash, buffer.Get(5).ComputeHash());
    }

    [Fact]
    public void SaveStoresACopy()
    {
        var buffer = new SnapshotBuffer(4);
        WorldData world = WorldAtFrame(0);
        ulong hash = buffer.Save(world);

        world.Fighters[0].Health = 1;

        Assert.Equal(hash, buffer.Get(0).ComputeHash());
    }

    [Fact]
    public void KeepsTheLastCapacityFrames()
    {
        var buffer = new SnapshotBuffer(4);
        for (int frame = 0; frame < 10; frame++)
        {
            buffer.Save(WorldAtFrame(frame));
        }

        for (int frame = 0; frame < 6; frame++)
        {
            Assert.False(buffer.Contains(frame));
        }
        for (int frame = 6; frame < 10; frame++)
        {
            Assert.True(buffer.Contains(frame));
            Assert.Equal(frame, buffer.Get(frame).Frame);
        }
        Assert.False(buffer.Contains(10));
    }

    [Fact]
    public void SavingAFrameAgainReplacesIt()
    {
        var buffer = new SnapshotBuffer(4);
        buffer.Save(WorldAtFrame(2));
        WorldData changed = WorldAtFrame(2);
        changed.Fighters[1].Health = 1;

        ulong hash = buffer.Save(changed);

        Assert.Equal(changed.ComputeHash(), hash);
        Assert.Equal(1, buffer.Get(2).Fighters[1].Health);
    }

    [Fact]
    public void ClearRemovesAllFrames()
    {
        var buffer = new SnapshotBuffer(4);
        buffer.Save(WorldAtFrame(0));
        buffer.Save(WorldAtFrame(1));

        buffer.Clear();

        Assert.False(buffer.Contains(0));
        Assert.False(buffer.Contains(1));
    }

    [Fact]
    public void RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotBuffer(0));
        var buffer = new SnapshotBuffer(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Save(WorldAtFrame(-1)));
        Assert.False(buffer.Contains(-1));
    }
}
