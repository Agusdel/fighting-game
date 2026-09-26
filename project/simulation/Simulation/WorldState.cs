using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

public enum MatchPhase : byte
{
    Fighting,
}

/// <summary>
/// The complete rollback state of a match. It is one value type with no references:
/// a snapshot is a copy (<c>WorldState snapshot = state;</c>), and a restore is a copy back.
/// </summary>
/// <remarks>
/// The hash reads each field explicitly and never the raw memory. Raw memory includes struct padding bytes,
/// and their content is not defined, so two equal states could give different raw-memory hashes.
/// </remarks>
public struct WorldState
{
    /// <summary>Number of ticks run since the match started.</summary>
    public int Frame;

    public FixedRng Rng;

    public MatchPhase Phase;

    /// <summary>Frames since <see cref="Phase"/> started.</summary>
    public int PhaseTimer;

    public FighterArray Fighters;

    /// <summary>Creates the state at frame 0. Fighters in slots 0 to playerCount - 1 are active.</summary>
    public static WorldState Create(GameData data, int playerCount, ulong seed)
    {
        StageData stage = data.Stage;
        if (playerCount < 1 || playerCount > GameConstants.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }
        if (stage.SpawnPoints.Length < playerCount)
        {
            throw new ArgumentException($"The stage has {stage.SpawnPoints.Length} spawn points, but {playerCount} players.");
        }

        var state = new WorldState
        {
            Rng = new FixedRng(seed),
            Phase = MatchPhase.Fighting,
        };

        Fixed centerX = (stage.Bounds.Min.X + stage.Bounds.Max.X) * Fixed.Half;
        for (int i = 0; i < playerCount; i++)
        {
            FixedVector2 spawn = stage.SpawnPoints[i];
            state.Fighters[i] = new FighterState
            {
                Active = true,
                Position = spawn,
                Facing = (sbyte)(spawn.X <= centerX ? 1 : -1),
                Action = FighterAction.Idle,
                Grounded = true,
                JumpsLeft = data.Fighter.MaxJumps,
            };
        }
        return state;
    }

    /// <summary>Adds every field. When you add a field to this struct, add it here too (a unit test checks this).</summary>
    public readonly void Hash(ref StateHasher hasher)
    {
        hasher.Add(Frame);
        Rng.Hash(ref hasher);
        hasher.Add((byte)Phase);
        hasher.Add(PhaseTimer);
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            Fighters[i].Hash(ref hasher);
        }
    }

    public readonly ulong ComputeHash()
    {
        var hasher = new StateHasher();
        Hash(ref hasher);
        return hasher.Value;
    }
}
