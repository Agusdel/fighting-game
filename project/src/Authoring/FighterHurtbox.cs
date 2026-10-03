using System;
using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// A box where the fighter can be hit. Hurtboxes are body parts: they always exist, outside the states.
/// The node position is the top-left corner, relative to the feet, for a fighter that faces right.
/// </summary>
/// <remarks>
/// A state animation can change the position, the size, and <see cref="Active"/> on some frames (discrete keys).
/// On the frames that a state does not key, the hurtbox has its value in the scene.
/// </remarks>
[Tool]
[GlobalClass]
public partial class FighterHurtbox : SnappedNode2D
{
    private Vector2I _size = new(48, 96);
    private bool _active = true;

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

    /// <summary>False = the box cannot be hit on this frame (for example a body part that moves out of reach).</summary>
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

    public override void _Draw()
    {
        if (Engine.IsEditorHint())
        {
            FighterAuthoring.DrawBox(this, _size, FighterAuthoring.HurtboxColor, _active);
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (FighterAuthoring.FindRoot(this) == null)
        {
            warnings.Add("A hurtbox must be below a FighterRoot.");
        }
        for (Node? node = GetParent(); node != null; node = node.GetParent())
        {
            if (node is FighterState)
            {
                warnings.Add("Hurtboxes are body parts: put them outside the states (for example in a \"Hurtboxes\" node). A state animation can move, resize, or turn them off.");
                break;
            }
        }
        return warnings.ToArray();
    }
}
