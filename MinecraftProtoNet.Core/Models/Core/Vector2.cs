using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace MinecraftProtoNet.Core.Models.Core;

public class Vector2<TNumber> where TNumber : INumber<TNumber>
{
    [SetsRequiredMembers]
    public Vector2()
    {
        X = default!;
        Y = default!;
    }

    [SetsRequiredMembers]
    public Vector2(TNumber x, TNumber y)
    {
        X = x;
        Y = y;
    }

    public required TNumber X { get; set; }
    public required TNumber Y { get; set; }

    public void Set(TNumber x, TNumber y)
    {
        X = x;
        Y = y;
    }

    public static Vector2<TNumber> operator +(Vector2<TNumber> a, Vector2<TNumber> b)
    {
        return new Vector2<TNumber>(a.X + b.X, a.Y + b.Y);
    }

    public static Vector2<TNumber> operator -(Vector2<TNumber> a, Vector2<TNumber> b)
    {
        return new Vector2<TNumber>(a.X - b.X, a.Y - b.Y);
    }

    public static Vector2<TNumber> operator *(Vector2<TNumber> a, TNumber scalar)
    {
        return new Vector2<TNumber>(a.X * scalar, a.Y * scalar);
    }

    public static Vector2<TNumber> operator *(TNumber scalar, Vector2<TNumber> a)
    {
        return a * scalar;
    }

    public double Length()
    {
        var x = Convert.ToDouble(X);
        var y = Convert.ToDouble(Y);
        return Math.Sqrt(x * x + y * y);
    }

    public Vector2<double> Normalized()
    {
        var length = Length();
        return length > 1e-5
            ? new Vector2<double>(Convert.ToDouble(X) / length, Convert.ToDouble(Y) / length)
            : new Vector2<double>(0, 0);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is Vector2<TNumber> other)
        {
            return X == other.X && Y == other.Y;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Vector2<TNumber>? left, Vector2<TNumber>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Vector2<TNumber>? left, Vector2<TNumber>? right)
    {
        return !(left == right);
    }
}
