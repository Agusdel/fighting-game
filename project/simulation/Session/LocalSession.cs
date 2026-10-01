using System;
using FightingGame.Core;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>A match where all players are on this machine: each frame runs once, with the inputs of all local players.</summary>
public sealed class LocalSession : IMatchSession
{
    private WorldData _world;
    private FrameInput _nextInput;

    public LocalSession(GameData data, int playerCount, ulong seed)
    {
        Data = data;
        PlayerCount = playerCount;
        _world = WorldData.Create(data, playerCount, seed);
    }

    public GameData Data { get; }

    public int PlayerCount { get; }

    public ref readonly WorldData World => ref _world;

    public void SetLocalInput(int slot, InputFlags input)
    {
        if (slot < 0 || slot >= PlayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} is not in the match ({PlayerCount} players).");
        }
        _nextInput[slot] = input;
    }

    /// <summary>Runs one frame with the inputs set since the last frame, then clears them.</summary>
    public void AdvanceFrame()
    {
        Simulator.Tick(ref _world, _nextInput, Data);
        _nextInput = default;
    }
}
