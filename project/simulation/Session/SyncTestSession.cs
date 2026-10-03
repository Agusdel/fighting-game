using System;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>
/// A debug session that checks rollback on one machine: all players are local (as in <see cref="LocalSession"/>),
/// and after each frame the session rolls back and runs the last frames again.
/// </summary>
/// <remarks>
/// Each <see cref="AdvanceFrame"/>, with F = the frame before the tick:
/// <list type="number">
/// <item>Store the inputs of frame F, run the tick, and save the snapshot and hash of frame F + 1.</item>
/// <item>Load the snapshot of frame R = max(0, F + 1 - <see cref="CheckDistance"/>).</item>
/// <item>Run frames R to F again with the stored inputs, and compare each hash with the saved hash of the first run.
/// A difference throws <see cref="SyncTestException"/>.</item>
/// <item>Continue from the state of the second run. So the match always plays on the rollback path,
/// and a restore bug cannot hide.</item>
/// </list>
/// A difference means that the simulation uses something that is not in <see cref="WorldData"/> (for example a static
/// field), that the session stores an input at the wrong frame, or that some code is not deterministic.
/// Each frame runs <see cref="CheckDistance"/> + 1 ticks.
/// After a <see cref="SyncTestException"/>, do not use the session again.
/// </remarks>
public sealed class SyncTestSession : IMatchSession
{
    /// <summary>The planned maximum rollback window of online matches.</summary>
    public const int DefaultCheckDistance = 8;

    private readonly SnapshotBuffer _snapshots;
    private readonly InputHistory _inputs;
    private WorldData _world;
    private FrameInput _nextInput;

    public SyncTestSession(GameData data, int playerCount, ulong seed, int checkDistance = DefaultCheckDistance)
    {
        if (checkDistance < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(checkDistance), "The check distance must be at least 1.");
        }

        Data = data;
        PlayerCount = playerCount;
        CheckDistance = checkDistance;
        // Frames current - checkDistance to current must be stored (checkDistance + 1 states),
        // and the inputs of frames current - checkDistance to current - 1 (checkDistance inputs).
        _snapshots = new SnapshotBuffer(checkDistance + 1);
        _inputs = new InputHistory(checkDistance);
        _world = WorldData.Create(data, playerCount, seed);
        _snapshots.Save(_world);
    }

    public GameData Data { get; }

    public int PlayerCount { get; }

    /// <summary>The number of frames that each frame rolls back.</summary>
    public int CheckDistance { get; }

    public ref readonly WorldData World => ref _world;

    public void SetLocalInput(int slot, InputFlags input)
    {
        if (slot < 0 || slot >= PlayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} is not in the match ({PlayerCount} players).");
        }
        _nextInput[slot] = input;
    }

    /// <summary>
    /// Runs one frame with the inputs set since the last frame and clears them, then checks the last frames again.
    /// Throws <see cref="SyncTestException"/> when a frame gives a different state the second time. Always returns true.
    /// </summary>
    public bool AdvanceFrame()
    {
        _inputs.Set(_world.Frame, _nextInput);
        _nextInput = default;
        Simulator.Tick(ref _world, _inputs.Get(_world.Frame), Data);
        _snapshots.Save(_world);

        _world = RunAgainFromSnapshot(Math.Max(0, _world.Frame - CheckDistance), _world.Frame);
        return true;
    }

    /// <summary>Loads the snapshot of <paramref name="fromFrame"/> and runs it to <paramref name="toFrame"/>. Checks each frame.</summary>
    private WorldData RunAgainFromSnapshot(int fromFrame, int toFrame)
    {
        WorldData world = _snapshots.Get(fromFrame);
        for (int frame = fromFrame; frame < toFrame; frame++)
        {
            Simulator.Tick(ref world, _inputs.Get(frame), Data);

            ulong expectedHash = _snapshots.HashOf(world.Frame);
            ulong actualHash = world.ComputeHash();
            if (actualHash != expectedHash)
            {
                throw new SyncTestException(world.Frame, expectedHash, actualHash,
                    WorldDataDiff.Compare(_snapshots.Get(world.Frame), world));
            }
        }
        return world;
    }
}
