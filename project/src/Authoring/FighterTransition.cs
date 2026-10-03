using System.Collections.Generic;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// A rule to change to another state: it applies when all conditions are true and the state frame is inside the
/// window. The parent decides where the rule applies: a <see cref="FighterState"/> (the state's own rules), or a
/// <see cref="FighterSharedTransitions"/> (the shared rules of all actionable states). Siblings are checked in
/// child order: the first rule that applies wins.
/// </summary>
[Tool]
[GlobalClass]
public partial class FighterTransition : Node
{
    private string _target = "";

    /// <summary>The state to change to.</summary>
    [Export]
    public string Target
    {
        get => _target;
        set
        {
            _target = value.Trim();
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>First state frame where the rule applies (inclusive).</summary>
    [Export(PropertyHint.Range, "0,999,1,or_greater")] public int FromFrame { get; set; }

    /// <summary>Last state frame where the rule applies (inclusive). -1 = no end.</summary>
    [Export(PropertyHint.Range, "-1,999,1,or_greater")] public int ToFrame { get; set; } = -1;

    /// <summary>All conditions must be true. No conditions = always true (inside the frame window).</summary>
    [Export] public Godot.Collections.Array<FighterCondition> Conditions { get; set; } = new();

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() == nameof(Target))
        {
            FighterAuthoring.SetNameSuggestions(property, FighterAuthoring.FindRoot(this)?.GetStateNames() ?? new List<string>());
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (GetParent() is not (FighterState or FighterSharedTransitions))
        {
            warnings.Add("A transition must be a direct child of a FighterState or a FighterSharedTransitions node.");
        }

        List<string> states = FighterAuthoring.FindRoot(this)?.GetStateNames() ?? new List<string>();
        if (_target.Length == 0)
        {
            warnings.Add("Select the target state.");
        }
        else if (!states.Contains(_target))
        {
            warnings.Add($"There is no state named '{_target}'.");
        }

        if (ToFrame >= 0 && ToFrame < FromFrame)
        {
            warnings.Add("ToFrame is before FromFrame: the rule never applies.");
        }
        for (int i = 0; i < Conditions.Count; i++)
        {
            FighterCondition? condition = Conditions[i];
            if (condition == null)
            {
                warnings.Add($"Condition {i} is empty.");
            }
            else if (condition.Type == ConditionType.Custom && !StateHookRegistry.Default.TryGetCondition(condition.Custom, out _))
            {
                warnings.Add($"Condition {i}: there is no custom condition named '{condition.Custom}' in the hook registry.");
            }
        }
        return warnings.ToArray();
    }
}
