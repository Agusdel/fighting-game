using FightingGame.Presentation;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// One spawn position. The node position is the fighter feet (bottom-center of the fighter collision box).
/// A child of a <see cref="SpawnPositionPair"/> is one half of that pair. Any other spawn position is a single spawn
/// position: a match with an odd number of players uses one of them.
/// The editor draws a feet marker and the outline of the fighter collision box.
/// </summary>
[Tool]
[GlobalClass]
public partial class SpawnPosition : StageNode
{
    public static readonly Color SingleColor = new(0.4f, 1f, 0.4f);

    private Vector2 _fighterSize;

    /// <summary>The fighter collision box size, for the editor drawing.</summary>
    public Vector2 FighterSize
    {
        get
        {
            if (_fighterSize == Vector2.Zero)
            {
                _fighterSize = DefaultGameData.CreateFighterStats().CollisionBoxSize.ToVector2();
            }
            return _fighterSize;
        }
    }

    public bool IsInPair => GetParent() is SpawnPositionPair;

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationEnterTree)
        {
            // The color depends on the parent (pair or single).
            QueueRedraw();
        }
    }

    protected override void OnLocalTransformChanged()
    {
        // Redraw also when the script is attached to a node that is already in the tree (it gets no enter-tree notification).
        QueueRedraw();
        // The pair draws a line to this node.
        (GetParent() as SpawnPositionPair)?.QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        Vector2 size = FighterSize;
        Color color = IsInPair ? SpawnPositionPair.PairColor : SingleColor;
        DrawRect(new Rect2(-size.X / 2, -size.Y, size.X, size.Y), color, filled: false);
        DrawCircle(Vector2.Zero, 5, color);
    }
}
