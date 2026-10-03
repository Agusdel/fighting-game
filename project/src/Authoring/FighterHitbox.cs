using System;
using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// A box that hits other fighters. It belongs to the <see cref="FighterState"/> that is its parent.
/// The node position is the top-left corner, relative to the feet, for a fighter that faces right.
/// </summary>
/// <remarks>
/// The state animation turns the hitbox on (<see cref="Active"/>) and moves or resizes it, with discrete keys
/// on the frames where it hits. The hit values (damage, knockback, ...) are the same on all frames: for a second
/// strength (for example a sweet spot), add a second hitbox. When two hitboxes of a state touch a target on the
/// same frame, the first one in the child order wins.
/// In the editor, only the hitboxes of the state whose animation is open are drawn.
/// </remarks>
[Tool]
[GlobalClass]
public partial class FighterHitbox : SnappedNode2D
{
    private Vector2I _size = new(32, 32);
    private bool _active;

    /// <summary>Width and height in whole pixels. The minimum is 1 x 1.</summary>
    [Export]
    public Vector2I Size
    {
        get => _size;
        set
        {
            _size = new Vector2I(Math.Max(1, value.X), Math.Max(1, value.Y));
            QueueRedraw();
        }
    }

    /// <summary>True on the frames where the box hits. False by default: the state animation turns it on.</summary>
    [Export]
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0,999,1,or_greater")] public int Damage { get; set; } = 5;

    /// <summary>Frames of hitstun for the target.</summary>
    [Export(PropertyHint.Range, "0,999,1,or_greater,suffix:frames")] public int HitstunFrames { get; set; } = 12;

    /// <summary>The new velocity of the target, in pixels per frame. X is relative to the attacker facing (+X = forward). Y points down.</summary>
    [Export(PropertyHint.None, "suffix:px/frame")] public Vector2 Knockback { get; set; } = new(4, -2);

    /// <summary>Frames that the attacker and the target freeze when the hit connects (if hitstop is enabled).</summary>
    [Export(PropertyHint.Range, "0,99,1,or_greater,suffix:frames")] public int HitstopFrames { get; set; } = 3;

    public override void _Draw()
    {
        if (!Engine.IsEditorHint() || GetParent() is not FighterState state)
        {
            return;
        }
        if (FighterAuthoring.FindRoot(this) is { } root && !root.IsStateShown(state.Name))
        {
            return;
        }
        FighterAuthoring.DrawBox(this, _size, FighterAuthoring.HitboxColor, _active);
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (GetParent() is not FighterState)
        {
            warnings.Add("A hitbox must be a direct child of a FighterState.");
        }
        return warnings.ToArray();
    }
}
