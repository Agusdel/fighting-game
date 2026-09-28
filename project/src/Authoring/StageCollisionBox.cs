using System;
using Godot;

namespace FightingGame.Authoring;

public enum StageCollisionType
{
    /// <summary>Blocks from all sides (floor, ceiling, walls).</summary>
    Solid,

    /// <summary>One-way platform: blocks only a fighter that falls onto its top edge.</summary>
    Platform,
}

/// <summary>
/// One axis-aligned collision box. The node position is the top-left corner of the box.
/// The box is drawn only in the editor. In the game, the art comes from visual nodes.
/// </summary>
[Tool]
[GlobalClass]
public partial class StageCollisionBox : StageNode
{
    private static readonly Color SolidColor = new(0.6f, 0.6f, 0.7f);
    private static readonly Color PlatformColor = new(1f, 0.6f, 0.2f);

    private Vector2I _size = new(64, 64);
    private StageCollisionType _type = StageCollisionType.Solid;

    /// <summary>Width and height in whole pixels. The minimum is 1 x 1.</summary>
    [Export]
    public Vector2I Size
    {
        get => _size;
        set
        {
            _size = new Vector2I(Math.Max(1, value.X), Math.Max(1, value.Y));
            QueueRedraw();
            NotifyStageChanged();
        }
    }

    [Export]
    public StageCollisionType Type
    {
        get => _type;
        set
        {
            _type = value;
            QueueRedraw();
            NotifyStageChanged();
        }
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        Color color = _type == StageCollisionType.Solid ? SolidColor : PlatformColor;
        var rect = new Rect2(Vector2.Zero, _size);
        DrawRect(rect, color with { A = 0.3f });
        DrawRect(rect, color, filled: false);
    }
}
