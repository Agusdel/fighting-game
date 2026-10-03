using System.Collections.Generic;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>An action that runs on one frame of the parent <see cref="FighterState"/> (for example a jump or a lunge).</summary>
[Tool]
[GlobalClass]
public partial class FighterFrameAction : Node
{
    private FrameActionType _type = FrameActionType.Jump;

    /// <summary>The state frame of the action (0 = the first frame).</summary>
    [Export(PropertyHint.Range, "0,999,1,or_greater")] public int Frame { get; set; }

    [Export]
    public FrameActionType Type
    {
        get => _type;
        set
        {
            _type = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>For the velocity actions, in pixels per frame. X is relative to the facing (+X = forward). Y points down.</summary>
    [Export(PropertyHint.None, "suffix:px/frame")] public Vector2 Value { get; set; }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        bool usesValue = _type is FrameActionType.SetVelocity or FrameActionType.SetVelocityX or FrameActionType.SetVelocityY;
        if (property["name"].AsString() == nameof(Value) && !usesValue)
        {
            property["usage"] = (int)PropertyUsageFlags.NoEditor;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (GetParent() is not FighterState)
        {
            warnings.Add("A frame action must be a direct child of a FighterState.");
        }
        return warnings.ToArray();
    }
}
