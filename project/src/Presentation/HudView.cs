using System.Collections.Generic;
using FightingGame.Core;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Draws one health bar for each active player (placeholder art), and the result of a round during
/// <see cref="MatchPhase.RoundOver"/>. The node position is the top-left corner of the first health bar.
/// Like the other views, it reads everything from the world data in <see cref="Refresh"/>.
/// </summary>
public partial class HudView : Node2D
{
    [Export] public Vector2 BarSize { get; set; } = new(200, 14);

    /// <summary>Horizontal distance from one player's bar to the next.</summary>
    [Export] public float Spacing { get; set; } = 270;

    /// <summary>The center of the round result text, relative to this node.</summary>
    [Export] public Vector2 ResultPosition { get; set; } = new(536, -300);

    private readonly bool[] _active = new bool[GameConstants.MaxPlayers];
    private readonly float[] _healthRatio = new float[GameConstants.MaxPlayers];
    private readonly int[] _health = new int[GameConstants.MaxPlayers];
    private IReadOnlyList<Color> _colors = System.Array.Empty<Color>();
    private string _result = "";

    public void Refresh(in WorldData world, FighterDefinitionData definition, IReadOnlyList<Color> playerColors)
    {
        _colors = playerColors;
        int maxHealth = definition.Stats.MaxHealth;
        int standing = 0;
        int winner = -1;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            FighterData fighter = world.Fighters[i];
            _active[i] = fighter.Active;
            _health[i] = fighter.Health;
            _healthRatio[i] = maxHealth > 0 ? Mathf.Clamp((float)fighter.Health / maxHealth, 0, 1) : 0;
            if (fighter.Active && fighter.Health > 0)
            {
                standing++;
                winner = i;
            }
        }

        _result = world.Phase != MatchPhase.RoundOver ? ""
            : standing == 1 ? $"P{winner + 1} wins"
            : standing == 0 ? "Draw"
            : "";

        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            if (!_active[i])
            {
                continue;
            }

            Color color = i < _colors.Count ? _colors[i] : Colors.White;
            var origin = new Vector2(i * Spacing, 0);
            DrawString(font, origin + new Vector2(0, BarSize.Y - 1), $"P{i + 1}", HorizontalAlignment.Left, -1, 14, color);

            var bar = new Rect2(origin + new Vector2(30, 0), BarSize);
            DrawRect(bar, new Color(0, 0, 0, 0.5f));
            DrawRect(new Rect2(bar.Position, new Vector2(BarSize.X * _healthRatio[i], BarSize.Y)), color);
            DrawRect(bar, Colors.White, filled: false, width: 1);
            DrawString(font, bar.Position + new Vector2(BarSize.X + 6, BarSize.Y - 1), _health[i].ToString(),
                HorizontalAlignment.Left, -1, 14, Colors.White);
        }

        if (_result.Length > 0)
        {
            const float Width = 400;
            DrawString(font, ResultPosition - new Vector2(Width / 2, 0), _result, HorizontalAlignment.Center, Width, 40, Colors.White);
        }
    }
}
