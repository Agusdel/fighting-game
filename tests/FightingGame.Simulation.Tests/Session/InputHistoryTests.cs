using System;
using FightingGame.Session;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Session;

public class InputHistoryTests
{
    private static FrameInput InputOf(int frame)
    {
        FrameInput input = default;
        input[0] = (InputFlags)(frame & 0x7F);
        input[3] = InputFlags.Jump;
        return input;
    }

    [Fact]
    public void IsEmptyAtStart()
    {
        var history = new InputHistory(4);
        Assert.False(history.Contains(0));
        Assert.Throws<InvalidOperationException>(() => history.Get(0));
    }

    [Fact]
    public void GetReturnsTheStoredInput()
    {
        var history = new InputHistory(4);
        history.Set(7, InputOf(7));

        Assert.True(history.Contains(7));
        Assert.Equal(InputOf(7)[0], history.Get(7)[0]);
        Assert.Equal(InputFlags.Jump, history.Get(7)[3]);
    }

    [Fact]
    public void KeepsTheLastCapacityFrames()
    {
        var history = new InputHistory(4);
        for (int frame = 0; frame < 10; frame++)
        {
            history.Set(frame, InputOf(frame));
        }

        for (int frame = 0; frame < 6; frame++)
        {
            Assert.False(history.Contains(frame));
        }
        for (int frame = 6; frame < 10; frame++)
        {
            Assert.True(history.Contains(frame));
            Assert.Equal(InputOf(frame)[0], history.Get(frame)[0]);
        }
    }

    [Fact]
    public void ClearRemovesAllFrames()
    {
        var history = new InputHistory(4);
        history.Set(0, InputOf(0));

        history.Clear();

        Assert.False(history.Contains(0));
    }

    [Fact]
    public void RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputHistory(0));
        var history = new InputHistory(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Set(-1, default));
        Assert.False(history.Contains(-1));
    }
}
