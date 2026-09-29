using System.Collections.Generic;
using FightingGame.Core;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Draws one fighter (placeholder art): its body box, facing, state name and state frame, and optionally its active
/// hurtboxes and hitboxes. The node origin is the fighter feet.
/// The view has no state of its own: <see cref="Refresh"/> reads everything from the fighter data,
/// so it can show any frame (also after a rollback or a jump in a replay).
/// </summary>
public partial class FighterView : Node2D
{
    private static readonly Color HurtboxColor = new(0.3f, 1f, 0.4f);
    private static readonly Color HitboxColor = new(1f, 0.2f, 0.2f);
    private static readonly Vector2 ProgressBarSize = new(48, 4);

    [Export] public Color BodyColor { get; set; } = Colors.White;

    private readonly List<Rect2> _hurtboxes = new();
    private readonly List<Rect2> _hitboxes = new();

    /// <summary>Parts of the state duration where a hitbox is active, as (start, end) fractions of the duration.</summary>
    private readonly List<Vector2> _activeSegments = new();

    /// <summary>Fraction of the state duration that has passed, or -1 if the state has no fixed duration.</summary>
    private float _progress = -1;
    private Rect2 _body;
    private int _facing = 1;
    private bool _grounded;
    private bool _intangible;
    private bool _showCombatBoxes;
    private string _label = "";

    public void Refresh(in FighterData fighter, FighterDefinitionData definition, bool showCombatBoxes)
    {
        Visible = fighter.Active;
        if (!fighter.Active)
        {
            return;
        }

        Position = fighter.Position.ToVector2();
        FighterStateData state = definition.States[fighter.StateId];

        Vector2 size = definition.Stats.CollisionBoxSize.ToVector2();
        _body = new Rect2(-size.X / 2, -size.Y, size.X, size.Y);
        _facing = fighter.Facing;
        _grounded = fighter.Grounded;
        _intangible = state.Has(StateFlags.Intangible);
        _label = $"{state.Name} {fighter.StateFrame}";

        _showCombatBoxes = showCombatBoxes;
        _hurtboxes.Clear();
        _hitboxes.Clear();
        if (showCombatBoxes)
        {
            foreach (HurtboxData hurtbox in definition.HurtboxesOf(state))
            {
                if (hurtbox.IsActive(fighter.StateFrame))
                {
                    _hurtboxes.Add(ToLocalRect(hurtbox.Box, fighter.Facing));
                }
            }
            foreach (HitboxData hitbox in state.Hitboxes)
            {
                if (hitbox.IsActive(fighter.StateFrame))
                {
                    _hitboxes.Add(ToLocalRect(hitbox.Box, fighter.Facing));
                }
            }
        }

        UpdateProgress(fighter, definition, state, showCombatBoxes);
        QueueRedraw();
    }

    /// <summary>
    /// The progress bar shows how much of a state with a fixed length has passed: the state duration,
    /// or the hitstun time of the last hit for the hitstun state. Red segments mark the frames with an active hitbox.
    /// </summary>
    private void UpdateProgress(in FighterData fighter, FighterDefinitionData definition, FighterStateData state, bool show)
    {
        _progress = -1;
        _activeSegments.Clear();
        if (!show)
        {
            return;
        }

        int duration = state.Duration > 0 ? state.Duration
            : fighter.StateId == definition.HitstunState ? fighter.HitstunFrames
            : 0;
        if (duration <= 0)
        {
            return;
        }

        // Frame 0 is the first of 'duration' frames, so after frame f, (f + 1) frames have passed.
        _progress = Mathf.Clamp((fighter.StateFrame + 1) / (float)duration, 0, 1);
        foreach (HitboxData hitbox in state.Hitboxes)
        {
            float start = Mathf.Clamp(hitbox.FromFrame / (float)duration, 0, 1);
            float end = Mathf.Clamp((hitbox.ToFrame + 1) / (float)duration, 0, 1);
            _activeSegments.Add(new Vector2(start, end));
        }
    }

    public override void _Draw()
    {
        Color color = _intangible ? BodyColor with { A = 0.3f } : BodyColor;
        DrawRect(_body, color with { A = color.A * 0.35f });
        if (!_showCombatBoxes)
        {
            // With the combat boxes on, the hurtbox outline (usually the same box) replaces the body outline.
            DrawRect(_body, color, filled: false, width: 2);
        }

        // Facing: a triangle at head height that points in the facing direction.
        float eyeY = _body.Position.Y + _body.Size.Y * 0.25f;
        float front = _facing * _body.Size.X / 2;
        DrawColoredPolygon(new[]
        {
            new Vector2(front, eyeY - 8),
            new Vector2(front + _facing * 12, eyeY),
            new Vector2(front, eyeY + 8),
        }, color);

        // Feet marker: filled when grounded.
        if (_grounded)
        {
            DrawCircle(Vector2.Zero, 4, color);
        }
        else
        {
            DrawArc(Vector2.Zero, 4, 0, Mathf.Tau, 12, color, 1);
        }

        foreach (Rect2 hurtbox in _hurtboxes)
        {
            DrawRect(hurtbox, HurtboxColor, filled: false, width: 2);
        }
        foreach (Rect2 hitbox in _hitboxes)
        {
            DrawRect(hitbox, HitboxColor with { A = 0.45f });
            DrawRect(hitbox, HitboxColor, filled: false, width: 1);
        }

        // Progress bar of a state with a fixed length, just above the body. The hitbox frames are a thin red strip
        // under the bar (a separate strip, so it stays visible when the player color is red too).
        // The space for the bar is always reserved, so the state name does not move when the bar appears.
        const float StripHeight = 2;
        var bar = new Rect2(-ProgressBarSize.X / 2, _body.Position.Y - 3 - StripHeight - ProgressBarSize.Y, ProgressBarSize);
        if (_progress >= 0)
        {
            DrawRect(bar, new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * _progress, bar.Size.Y)), color with { A = 0.9f });
            foreach (Vector2 segment in _activeSegments)
            {
                DrawRect(new Rect2(bar.Position.X + bar.Size.X * segment.X, bar.End.Y,
                    bar.Size.X * (segment.Y - segment.X), StripHeight), HitboxColor);
            }
        }
        float labelBottom = bar.Position.Y - 3;

        // State name and frame above the progress bar space.
        const float LabelWidth = 200;
        DrawString(ThemeDB.FallbackFont, new Vector2(-LabelWidth / 2, labelBottom), _label,
            HorizontalAlignment.Center, LabelWidth, 12, color);
    }

    /// <summary>A box relative to the feet (defined for a fighter that faces right), mirrored for the facing.</summary>
    private static Rect2 ToLocalRect(FixedAABB box, int facing) =>
        (facing < 0 ? box.MirrorX(Fixed.Zero) : box).ToRect2();
}
