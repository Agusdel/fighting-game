using System;
using System.Diagnostics;

namespace FightingGame.Core;

/// <summary>
/// Axis-aligned bounding box with <see cref="Fixed"/> coordinates. Invariant: Min &lt;= Max on both axes.
/// </summary>
public readonly struct FixedAABB : IEquatable<FixedAABB>
{
    public readonly FixedVector2 Min;
    public readonly FixedVector2 Max;

    public FixedAABB(FixedVector2 min, FixedVector2 max)
    {
        Debug.Assert(min.X <= max.X && min.Y <= max.Y, "FixedAABB: Min must be <= Max.");
        Min = min;
        Max = max;
    }

    public static FixedAABB FromMinSize(FixedVector2 min, FixedVector2 size) => new(min, min + size);

    /// <summary>
    /// Creates a box from its center and size. The half size rounds toward negative infinity,
    /// so use even sizes to keep the box centered exactly.
    /// </summary>
    public static FixedAABB FromCenterSize(FixedVector2 center, FixedVector2 size)
    {
        FixedVector2 half = size * Fixed.Half;
        return new(center - half, center - half + size);
    }

    public Fixed Width => Max.X - Min.X;
    public Fixed Height => Max.Y - Min.Y;
    public FixedVector2 Size => Max - Min;

    /// <summary>
    /// True if the interiors overlap. Boxes that only touch at an edge do not overlap
    /// (a fighter that stands on the floor does not overlap the floor).
    /// </summary>
    public bool Overlaps(FixedAABB other) =>
        Min.X < other.Max.X && other.Min.X < Max.X &&
        Min.Y < other.Max.Y && other.Min.Y < Max.Y;

    public FixedAABB Translate(FixedVector2 offset) => new(Min + offset, Max + offset);

    /// <summary>Mirrors the box across the vertical line x = <paramref name="originX"/>.</summary>
    public FixedAABB MirrorX(Fixed originX)
    {
        Fixed twice = originX + originX;
        return new(new FixedVector2(twice - Max.X, Min.Y), new FixedVector2(twice - Min.X, Max.Y));
    }

    public static bool operator ==(FixedAABB a, FixedAABB b) => a.Min == b.Min && a.Max == b.Max;
    public static bool operator !=(FixedAABB a, FixedAABB b) => !(a == b);

    public bool Equals(FixedAABB other) => this == other;
    public override bool Equals(object? obj) => obj is FixedAABB other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public override string ToString() => $"[{Min} - {Max}]";
}
