using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// The static definition of one kind of fighter: its movement values and all its states.
/// <see cref="FighterData"/> is one fighter in a match; this is what it is made of.
/// Build it with <see cref="FighterDefinitionBuilder"/>.
/// </summary>
public sealed class FighterDefinitionData
{
    public required FighterStats Stats { get; init; }

    /// <summary>All states. The index in the array is the state id.</summary>
    public required FighterStateData[] States { get; init; }

    /// <summary>Transitions for <see cref="StateFlags.Actionable"/> states while grounded. Checked before the state's own transitions.</summary>
    public required TransitionData[] SharedGroundTransitions { get; init; }

    /// <summary>Transitions for <see cref="StateFlags.Actionable"/> states while airborne. Checked before the state's own transitions.</summary>
    public required TransitionData[] SharedAirTransitions { get; init; }

    /// <summary>The state of a fighter when it spawns.</summary>
    public required ushort IdleState { get; init; }

    /// <summary>Returns the id of the state with this name. Throws if there is no such state. Not for use in a tick (it searches the array).</summary>
    public ushort FindState(string name)
    {
        foreach (FighterStateData state in States)
        {
            if (state.Name == name)
            {
                return state.Id;
            }
        }
        throw new ArgumentException($"There is no state named '{name}'.", nameof(name));
    }

    /// <summary>
    /// Hash of all definition data. Two machines with the same hash have the same fighter definition.
    /// Hooks and custom conditions are hashed by their method name (type and method), because code cannot be hashed.
    /// </summary>
    public ulong ComputeHash()
    {
        var hasher = new StateHasher();
        Stats.Hash(ref hasher);
        hasher.Add(IdleState);

        hasher.Add(States.Length);
        foreach (FighterStateData state in States)
        {
            hasher.Add(state.Id);
            hasher.Add(state.Name);
            hasher.Add(state.Duration);
            hasher.Add(state.NextState);
            hasher.Add((byte)state.Movement);
            hasher.Add((ushort)state.Flags);
            hasher.Add(state.OnLanding);
            hasher.Add(state.OnLeaveGround);

            hasher.Add(state.FrameActions.Length);
            foreach (FrameActionData action in state.FrameActions)
            {
                hasher.Add(action.Frame);
                hasher.Add((byte)action.Type);
                hasher.Add(action.Value);
            }

            HashTransitions(ref hasher, state.Transitions);
            HashDelegate(ref hasher, state.OnEnter);
            HashDelegate(ref hasher, state.OnUpdate);
            HashDelegate(ref hasher, state.OnExit);
        }

        HashTransitions(ref hasher, SharedGroundTransitions);
        HashTransitions(ref hasher, SharedAirTransitions);
        return hasher.Value;
    }

    private static void HashTransitions(ref StateHasher hasher, TransitionData[] transitions)
    {
        hasher.Add(transitions.Length);
        foreach (TransitionData transition in transitions)
        {
            hasher.Add(transition.TargetState);
            hasher.Add(transition.FromFrame);
            hasher.Add(transition.ToFrame);
            hasher.Add(transition.Conditions.Length);
            foreach (ConditionData condition in transition.Conditions)
            {
                hasher.Add((byte)condition.Type);
                hasher.Add((ushort)condition.Buttons);
                hasher.Add((byte)condition.Direction);
                HashDelegate(ref hasher, condition.Custom);
            }
        }
    }

    private static void HashDelegate(ref StateHasher hasher, Delegate? method)
    {
        hasher.Add(method == null
            ? string.Empty
            : $"{method.Method.DeclaringType?.FullName}.{method.Method.Name}");
    }
}
