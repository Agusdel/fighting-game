using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>Shared helpers of the fighter authoring nodes (editor drawing, dropdowns, warnings).</summary>
internal static class FighterAuthoring
{
    // The same colors as the debug drawing in the game.
    public static readonly Color HurtboxColor = new(0.3f, 1f, 0.4f);
    public static readonly Color HitboxColor = new(1f, 0.2f, 0.2f);
    public static readonly Color CollisionBoxColor = new(0.3f, 0.6f, 1f);

    /// <summary>The <see cref="FighterRoot"/> above the node, or null.</summary>
    public static FighterRoot? FindRoot(Node node)
    {
        for (Node? current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current is FighterRoot root)
            {
                return root;
            }
        }
        return null;
    }

    /// <summary>
    /// Makes a string property a dropdown of names. The value can still be typed, and an empty value is allowed
    /// (for example "no state"). Call it from <c>_ValidateProperty</c>.
    /// </summary>
    public static void SetNameSuggestions(Godot.Collections.Dictionary property, IEnumerable<string> names)
    {
        property["hint"] = (int)PropertyHint.EnumSuggestion;
        property["hint_string"] = string.Join(",", names);
    }

    /// <summary>Draws an authored box: a filled rectangle and an outline. An inactive box is only a faint outline.</summary>
    public static void DrawBox(CanvasItem item, Vector2I size, Color color, bool active)
    {
        var rect = new Rect2(Vector2.Zero, size);
        if (active)
        {
            item.DrawRect(rect, color with { A = 0.25f });
            item.DrawRect(rect, color, filled: false, width: 1);
        }
        else
        {
            item.DrawRect(rect, color with { A = 0.35f }, filled: false, width: 1);
        }
    }
}
