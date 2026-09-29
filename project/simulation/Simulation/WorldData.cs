using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

public enum MatchPhase : byte
{
    /// <summary>The round is running.</summary>
    Fighting,

    /// <summary>At most one fighter is left. The fighters still run; the next round starts after a delay.</summary>
    RoundOver,
}

/// <summary>
/// The complete rollback state of a match. It is one value type with no references:
/// a snapshot is a copy (<c>WorldData snapshot = state;</c>), and a restore is a copy back.
/// </summary>
/// <remarks>
/// The hash reads each field explicitly and never the raw memory. Raw memory includes struct padding bytes,
/// and their content is not defined, so two equal states could give different raw-memory hashes.
/// </remarks>
public struct WorldData
{
    /// <summary>Number of ticks run since the match started. It never resets (also not at a new round).</summary>
    public int Frame;

    public FixedRng Rng;

    /// <summary>The number of the current round, from 1.</summary>
    public int Round;

    public MatchPhase Phase;

    /// <summary>Frames since <see cref="Phase"/> started.</summary>
    public int PhaseTimer;

    public FighterArray Fighters;

    /// <summary>
    /// Creates the state at frame 0 and starts round 1. Fighters in slots 0 to playerCount - 1 are active.
    /// </summary>
    public static WorldData Create(GameData data, int playerCount, ulong seed)
    {
        if (playerCount < 1 || playerCount > GameConstants.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        var state = new WorldData
        {
            Rng = new FixedRng(seed),
        };
        for (int i = 0; i < playerCount; i++)
        {
            state.Fighters[i].Active = true;
        }

        StartRound(ref state, data);
        return state;
    }

    /// <summary>
    /// Starts the next round: every active fighter gets full health, the idle state, and a new spawn position.
    /// The spawn positions are chosen at random with <see cref="SpawnPositionSelector"/> and <see cref="Rng"/>,
    /// so they are different in each round, but the same on every machine.
    /// The active fighters must be in slots 0 to (count - 1).
    /// </summary>
    public static void StartRound(ref WorldData state, GameData data)
    {
        int playerCount = 0;
        while (playerCount < GameConstants.MaxPlayers && state.Fighters[playerCount].Active)
        {
            playerCount++;
        }

        state.Round++;
        state.Phase = MatchPhase.Fighting;
        state.PhaseTimer = 0;

        StageData stage = data.Stage;
        Span<FixedVector2> spawnPositions = stackalloc FixedVector2[GameConstants.MaxPlayers];
        SpawnPositionSelector.Select(stage, playerCount, ref state.Rng, spawnPositions);

        FighterDefinitionData definition = data.FighterDefinition;
        Fixed centerX = (stage.Bounds.Min.X + stage.Bounds.Max.X) * Fixed.Half;
        for (int i = 0; i < playerCount; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            fighter = new FighterData
            {
                Active = true,
                Position = spawnPositions[i],
                Facing = (sbyte)(spawnPositions[i].X <= centerX ? 1 : -1),
                Grounded = true,
                JumpsLeft = definition.Stats.MaxJumps,
                Health = definition.Stats.MaxHealth,
                // Keep the last input: a button held across the restart must not count as pressed on the first frame.
                PrevInput = fighter.PrevInput,
            };
            FighterStateMachine.EnterInitial(ref fighter, definition.IdleState, Simulator.CreateContext(fighter, InputFlags.None, data));
        }
    }

    /// <summary>Adds every field. When you add a field to this struct, add it here too (a unit test checks this).</summary>
    public readonly void Hash(ref StateHasher hasher)
    {
        hasher.Add(Frame);
        Rng.Hash(ref hasher);
        hasher.Add(Round);
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
