using System;

namespace FightingGame.Core;

/// <summary>
/// 2D vector with <see cref="Fixed"/> components. Y points down (same as Godot).
/// </summary>
public readonly struct FixedVector2 : IEquatable<FixedVector2>
{
    public static readonly FixedVector2 Zero = new(Fixed.Zero, Fixed.Zero);

    public readonly Fixed X;
    public readonly Fixed Y;

    public FixedVector2(Fixed x, Fixed y)
    {
        X = x;
        Y = y;
    }

    public FixedVector2 WithX(Fixed x) => new(x, Y);
    public FixedVector2 WithY(Fixed y) => new(X, y);

    public static FixedVector2 operator +(FixedVector2 a, FixedVector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static FixedVector2 operator -(FixedVector2 a, FixedVector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static FixedVector2 operator -(FixedVector2 a) => new(-a.X, -a.Y);
    public static FixedVector2 operator *(FixedVector2 a, Fixed s) => new(a.X * s, a.Y * s);
    public static FixedVector2 operator *(Fixed s, FixedVector2 a) => new(a.X * s, a.Y * s);
    public static FixedVector2 operator /(FixedVector2 a, Fixed s) => new(a.X / s, a.Y / s);

    public static bool operator ==(FixedVector2 a, FixedVector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(FixedVector2 a, FixedVector2 b) => !(a == b);

    public bool Equals(FixedVector2 other) => this == other;
    public override bool Equals(object? obj) => obj is FixedVector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}
