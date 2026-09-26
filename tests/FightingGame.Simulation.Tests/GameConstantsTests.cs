using FightingGame.Core;
using Xunit;

namespace FightingGame.Simulation.Tests;

public class GameConstantsTests
{
    [Fact]
    public void MaxPlayersIsFour()
    {
        Assert.Equal(4, GameConstants.MaxPlayers);
    }

    [Fact]
    public void TickRateIsSixty()
    {
        Assert.Equal(60, GameConstants.TickRate);
    }
}
