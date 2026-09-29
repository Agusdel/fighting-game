using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

public enum MatchPhase : byte
{
    Fighting,
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
    /// <summary>Number of ticks run since the match started.</summary>
    public int Frame;

    public FixedRng Rng;

    public MatchPhase Phase;

    /// <summary>Frames since <see cref="Phase"/> started.</summary>
    public int PhaseTimer;

    public FighterArray Fighters;

    /// <summary>
    /// Creates the state at frame 0. Fighters in slots 0 to playerCount - 1 are active.
    /// The spawn positions are chosen at random with <see cref="SpawnPositionSelector"/> and the seeded <see cref="Rng"/>.
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
            Phase = MatchPhase.Fighting,
        };

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
            };
            FighterStateMachine.EnterInitial(ref fighter, definition.IdleState, Simulator.CreateContext(fighter, InputFlags.None, data));
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
