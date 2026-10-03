using System.Collections.Generic;
using FightingGame.PlayerInput;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.App;

/// <summary>
/// Everything a match needs before it starts: the players and their input devices, the seed, the stage, and the rules.
/// The lobby makes it; it does not change during the match. The rounds of a match are part of the simulation state,
/// so nothing else must be kept between rounds.
/// </summary>
public sealed class MatchSetup
{
    /// <summary>The input device of each player slot. Index = slot. The count is the number of players.</summary>
    public required IReadOnlyList<InputDevice> SlotDevices { get; init; }

    /// <summary>
    /// The seed of the match random generator (spawn positions). It is chosen once, outside the simulation,
    /// and is the same for the whole match.
    /// </summary>
    public required ulong Seed { get; init; }

    /// <summary>A stage scene. Its root must be a <c>StageRoot</c> (stage scenes inherit StageBase.tscn).</summary>
    public required PackedScene StageScene { get; init; }

    public MatchRulesData Rules { get; init; } = new();

    public MatchSessionType SessionType { get; init; } = MatchSessionType.Local;

    /// <summary>The simulated network when <see cref="SessionType"/> is <see cref="MatchSessionType.Loopback"/>.</summary>
    public LoopbackSettings Loopback { get; init; } = new(LatencyMs: 50, JitterMs: 10, LossPercent: 2);

    public int PlayerCount => SlotDevices.Count;

    /// <summary>A new random seed for a match. Not used inside the simulation.</summary>
    public static ulong NewSeed() => (ulong)System.Random.Shared.NextInt64();
}
