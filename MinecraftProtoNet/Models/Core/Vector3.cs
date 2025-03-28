using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector3<TNumber> where TNumber : INumber<TNumber>
{
    public Vector3()
    {
    }

    public Vector3(TNumber x, TNumber y, TNumber z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public TNumber X { get; set; }
    public TNumber Y { get; set; }
    public TNumber Z { get; set; }

    public void Set(TNumber x, TNumber y, TNumber z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3<TNumber> operator +(Vector3<TNumber> a, Vector3<TNumber> b)
    {
        return new Vector3<TNumber>(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public static Vector3<TNumber> operator -(Vector3<TNumber> a, Vector3<TNumber> b)
    {
        return new Vector3<TNumber>(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    public static Vector3<TNumber> operator *(Vector3<TNumber> a, TNumber scalar)
    {
        return new Vector3<TNumber>(a.X * scalar, a.Y * scalar, a.Z * scalar);
    }

    public static Vector3<TNumber> operator *(TNumber scalar, Vector3<TNumber> a)
    {
        return a * scalar;
    }

    public double Length()
    {
        var x = Convert.ToDouble(X);
        var y = Convert.ToDouble(Y);
        var z = Convert.ToDouble(Z);
        return Math.Sqrt(x * x + y * y + z * z);
    }

    public double LengthSquared()
    {
        var x = Convert.ToDouble(X);
        var y = Convert.ToDouble(Y);
        var z = Convert.ToDouble(Z);
        return x * x + y * y + z * z;
    }


    public Vector3<double> Normalized()
    {
        var length = Length();
        const double epsilon = 1e-10;
        if (length < epsilon)
        {
            return new Vector3<double>(0, 0, 0);
        }

        var invLength = 1.0 / length;
        return new Vector3<double>(
            Convert.ToDouble(X) * invLength,
            Convert.ToDouble(Y) * invLength,
            Convert.ToDouble(Z) * invLength
        );
    }

    public TNumber Dot(Vector3<TNumber> other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }

    public Vector3<TNumber> Cross(Vector3<TNumber> other)
    {
        return new Vector3<TNumber>(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X
        );
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public static Vector3<TNumber> Zero => new(TNumber.Zero, TNumber.Zero, TNumber.Zero);
}
