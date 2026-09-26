using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Draws one fighter as its collision box (placeholder art). The node origin is the fighter feet.
/// The view has no state of its own: <see cref="Refresh"/> reads everything from the fighter state,
/// so it can show any frame (also after a rollback or a jump in a replay).
/// </summary>
public partial class FighterView : Node2D
{
    [Export] public Color BodyColor { get; set; } = Colors.White;

    private Rect2 _box;
    private int _facing = 1;
    private bool _grounded;

    public void Refresh(in FighterState fighter, FighterStats stats)
    {
        Visible = fighter.Active;
        Position = fighter.Position.ToVector2();

        Vector2 size = stats.CollisionBoxSize.ToVector2();
        var box = new Rect2(-size.X / 2, -size.Y, size.X, size.Y);
        if (box != _box || fighter.Facing != _facing || fighter.Grounded != _grounded)
        {
            _box = box;
            _facing = fighter.Facing;
            _grounded = fighter.Grounded;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(_box, BodyColor with { A = 0.35f });
        DrawRect(_box, BodyColor, filled: false, width: 2);

        // Facing: a triangle at head height that points in the facing direction.
        float eyeY = _box.Position.Y + _box.Size.Y * 0.25f;
        float front = _facing * _box.Size.X / 2;
        DrawColoredPolygon(new[]
        {
            new Vector2(front, eyeY - 8),
            new Vector2(front + _facing * 12, eyeY),
            new Vector2(front, eyeY + 8),
        }, BodyColor);

        // Feet marker: filled when grounded.
        if (_grounded)
        {
            DrawCircle(Vector2.Zero, 4, BodyColor);
        }
        else
        {
            DrawArc(Vector2.Zero, 4, 0, Mathf.Tau, 12, BodyColor, 1);
        }
    }
}
