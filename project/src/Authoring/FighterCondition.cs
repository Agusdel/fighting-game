using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// One condition of a <see cref="FighterTransition"/>. The inspector shows only the values that the
/// <see cref="Type"/> uses.
/// </summary>
[Tool]
[GlobalClass]
public partial class FighterCondition : Resource
{
    private ConditionType _type = ConditionType.InputPressed;

    [Export]
    public ConditionType Type
    {
        get => _type;
        set
        {
            _type = value;
            NotifyPropertyListChanged();
            EmitChanged();
        }
    }

    /// <summary>For the input conditions: all these buttons must be pressed, held, or released.</summary>
    [Export] public InputFlags Buttons { get; set; } = InputFlags.None;

    /// <summary>For <see cref="ConditionType.Direction"/>. Forward and back are relative to the fighter facing.</summary>
    [Export] public DirectionInput Direction { get; set; } = DirectionInput.Horizontal;

    /// <summary>For <see cref="ConditionType.Custom"/>: a condition name from the hook registry.</summary>
    [Export] public string Custom { get; set; } = "";

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name = property["name"].AsString();
        bool used = name switch
        {
            nameof(Buttons) => _type is ConditionType.InputPressed or ConditionType.InputHeld or ConditionType.InputReleased,
            nameof(Direction) => _type == ConditionType.Direction,
            nameof(Custom) => _type == ConditionType.Custom,
            _ => true,
        };
        if (!used)
        {
            // Keep the value in the file, but do not show it.
            property["usage"] = (int)PropertyUsageFlags.NoEditor;
        }
        if (name == nameof(Custom))
        {
            FighterAuthoring.SetNameSuggestions(property, StateHookRegistry.Default.ConditionNames);
        }
    }

    /// <summary>A short text for warnings, for example "InputPressed(Jump)".</summary>
    public string Describe() => _type switch
    {
        ConditionType.InputPressed or ConditionType.InputHeld or ConditionType.InputReleased => $"{_type}({Buttons})",
        ConditionType.Direction => $"Direction({Direction})",
        ConditionType.Custom => $"Custom({Custom})",
        _ => _type.ToString(),
    };
}
