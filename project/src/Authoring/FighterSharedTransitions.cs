using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

public enum FighterSharedTransitionType
{
    /// <summary>The rules apply to actionable states while the fighter is grounded.</summary>
    Ground,

    /// <summary>The rules apply to actionable states while the fighter is airborne.</summary>
    Air,
}

/// <summary>
/// A list of shared transitions (the <see cref="FighterTransition"/> children). They apply to all states with the
/// <see cref="StateFlags.Actionable"/> flag, before the state's own transitions.
/// </summary>
[Tool]
[GlobalClass]
public partial class FighterSharedTransitions : Node
{
    [Export] public FighterSharedTransitionType Type { get; set; } = FighterSharedTransitionType.Ground;
}
