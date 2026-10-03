using System.Collections.Generic;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// One state of the fighter state machine. The node name is the state name; the state animation in the
/// <c>AnimationPlayer</c> has the same name.
/// </summary>
/// <remarks>
/// Children: <see cref="FighterHitbox"/> (the hitboxes of the state), <see cref="FighterTransition"/> (the state's
/// own rules to change to another state, in priority order), <see cref="FighterFrameAction"/>.
/// References to other states are names, with a dropdown of the states of the fighter. An empty name = no state.
/// </remarks>
[Tool]
[GlobalClass]
public partial class FighterState : SnappedNode2D
{
    private string _nextState = "";
    private string _nextStateInAir = "";
    private string _onLanding = "";
    private string _onLeaveGround = "";
    private string _onEnter = "";
    private string _onUpdate = "";
    private string _onExit = "";

    /// <summary>The number of frames of the state. 0 = no end (the state ends only with a transition).</summary>
    [Export(PropertyHint.Range, "0,999,1,or_greater,suffix:frames")] public int Duration { get; set; }

    /// <summary>The state after <see cref="Duration"/> ends, when the fighter is grounded.</summary>
    [Export] public string NextState { get => _nextState; set => SetName(ref _nextState, value); }

    /// <summary>The state after <see cref="Duration"/> ends, when the fighter is airborne. Empty = <see cref="NextState"/>.</summary>
    [Export] public string NextStateInAir { get => _nextStateInAir; set => SetName(ref _nextStateInAir, value); }

    [Export] public MovementMode Movement { get; set; } = MovementMode.None;

    [Export] public StateFlags Flags { get; set; } = StateFlags.None;

    /// <summary>The state to change to when the fighter lands. Empty = stay in this state.</summary>
    [Export] public string OnLanding { get => _onLanding; set => SetName(ref _onLanding, value); }

    /// <summary>The state to change to when the fighter leaves the ground without a jump. Empty = stay in this state.</summary>
    [Export] public string OnLeaveGround { get => _onLeaveGround; set => SetName(ref _onLeaveGround, value); }

    [ExportGroup("Hooks")]
    /// <summary>A hook (by name, from the hook registry) that runs when the state starts.</summary>
    [Export] public string OnEnter { get => _onEnter; set => SetName(ref _onEnter, value); }

    /// <summary>A hook that runs on each frame of the state.</summary>
    [Export] public string OnUpdate { get => _onUpdate; set => SetName(ref _onUpdate, value); }

    /// <summary>A hook that runs when the state ends.</summary>
    [Export] public string OnExit { get => _onExit; set => SetName(ref _onExit, value); }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name = property["name"].AsString();
        switch (name)
        {
            case nameof(NextState):
            case nameof(NextStateInAir):
            case nameof(OnLanding):
            case nameof(OnLeaveGround):
                FighterAuthoring.SetNameSuggestions(property, FighterAuthoring.FindRoot(this)?.GetStateNames() ?? new List<string>());
                break;
            case nameof(OnEnter):
            case nameof(OnUpdate):
            case nameof(OnExit):
                FighterAuthoring.SetNameSuggestions(property, StateHookRegistry.Default.HookNames);
                break;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        FighterRoot? root = FighterAuthoring.FindRoot(this);
        if (root == null)
        {
            warnings.Add("A state must be below a FighterRoot.");
            return warnings.ToArray();
        }

        List<string> states = root.GetStateNames();
        CheckStateName(warnings, states, nameof(NextState), _nextState);
        CheckStateName(warnings, states, nameof(NextStateInAir), _nextStateInAir);
        CheckStateName(warnings, states, nameof(OnLanding), _onLanding);
        CheckStateName(warnings, states, nameof(OnLeaveGround), _onLeaveGround);
        if (Duration > 0 && _nextState.Length == 0)
        {
            warnings.Add("The state has a duration, but no NextState.");
        }
        if (Duration == 0 && (_nextState.Length > 0 || _nextStateInAir.Length > 0))
        {
            warnings.Add("NextState is used only when the state has a duration.");
        }

        CheckHookName(warnings, nameof(OnEnter), _onEnter);
        CheckHookName(warnings, nameof(OnUpdate), _onUpdate);
        CheckHookName(warnings, nameof(OnExit), _onExit);
        return warnings.ToArray();
    }

    private static void CheckStateName(List<string> warnings, List<string> states, string property, string value)
    {
        if (value.Length > 0 && !states.Contains(value))
        {
            warnings.Add($"{property}: there is no state named '{value}'.");
        }
    }

    private static void CheckHookName(List<string> warnings, string property, string value)
    {
        if (value.Length > 0 && !StateHookRegistry.Default.TryGetHook(value, out _))
        {
            warnings.Add($"{property}: there is no hook named '{value}' in the hook registry.");
        }
    }

    private void SetName(ref string field, string value)
    {
        field = value.Trim();
        UpdateConfigurationWarnings();
    }
}
