using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace MinecraftProtoNet.Core.Models.Core;

public class Vector3<TNumber> : IFormattable where TNumber : INumber<TNumber>
{
    [SetsRequiredMembers]
    public Vector3()
    {
        X = default!;
        Y = default!;
        Z = default!;
    }

    [SetsRequiredMembers]
    public Vector3(TNumber x, TNumber y, TNumber z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public required TNumber X { get; set; }
    public required TNumber Y { get; set; }
    public required TNumber Z { get; set; }

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

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        formatProvider ??= CultureInfo.CurrentCulture;
        format = string.IsNullOrEmpty(format) ? "N2" : format;

        var xStr = X.ToString(format, formatProvider);
        var yStr = Y.ToString(format, formatProvider);
        var zStr = Z.ToString(format, formatProvider);

        return $"({xStr}, {yStr}, {zStr})";
    }

    public override string ToString()
    {
        return ToString("N4", CultureInfo.CurrentCulture);
    }

    public static Vector3<TNumber> Zero => new(TNumber.Zero, TNumber.Zero, TNumber.Zero);

    public override bool Equals(object? obj)
    {
        if (obj is Vector3<TNumber> other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public static bool operator ==(Vector3<TNumber>? left, Vector3<TNumber>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Vector3<TNumber>? left, Vector3<TNumber>? right)
    {
        return !(left == right);
    }
}
