using FightingGame.Core;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Conversions from fixed-point values to Godot float types. Use them for display only.
/// A float value must never go back into the simulation, because float results can differ between machines.
/// </summary>
public static class FixedExtensions
{
    public static float ToFloat(this Fixed value) => (float)((double)value.Raw / Fixed.OneRaw);

    public static Vector2 ToVector2(this FixedVector2 value) => new(value.X.ToFloat(), value.Y.ToFloat());

    public static Rect2 ToRect2(this FixedAABB box) => new(box.Min.ToVector2(), box.Size.ToVector2());
}
