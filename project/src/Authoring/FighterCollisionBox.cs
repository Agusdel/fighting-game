using System;
using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// The box that collides with the stage (floors, walls, platforms). It is always centered on the feet on X, and goes
/// up from the feet on Y, so only its size is authored. It does not change between states.
/// </summary>
[Tool]
[GlobalClass]
public partial class FighterCollisionBox : SnappedNode2D
{
    private Vector2I _size = new(48, 96);

    /// <summary>Width and height in whole pixels. Use an even width, so the box is centered exactly on the feet.</summary>
    [Export]
    public Vector2I Size
    {
        get => _size;
        set
        {
            _size = new Vector2I(Math.Max(2, value.X), Math.Max(1, value.Y));
            QueueRedraw();
            UpdateConfigurationWarnings();
        }
    }

    public override void _Draw()
    {
        if (Engine.IsEditorHint())
        {
            var rect = new Rect2(new Vector2(-_size.X / 2f, -_size.Y), _size);
            DrawRect(rect, FighterAuthoring.CollisionBoxColor, filled: false, width: 1);
        }
    }

    protected override void OnLocalTransformChanged() => UpdateConfigurationWarnings();

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (GetParent() is not FighterRoot)
        {
            warnings.Add("The collision box must be a direct child of the FighterRoot.");
        }
        if (Position != Vector2.Zero)
        {
            warnings.Add("The collision box is always at the feet: set its position to (0, 0).");
        }
        if (_size.X % 2 != 0)
        {
            warnings.Add("Use an even width, so the box is centered exactly on the feet.");
        }
        return warnings.ToArray();
    }
}
