using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector2<TNumber> where TNumber : INumber<TNumber>
{
    public Vector2()
    {
    }

    public Vector2(TNumber x, TNumber y)
    {
        X = x;
        Y = y;
    }

    public TNumber X { get; set; }
    public TNumber Y { get; set; }

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
}
