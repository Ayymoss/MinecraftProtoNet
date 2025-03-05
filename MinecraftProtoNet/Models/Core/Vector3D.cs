using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector3D
{
    public Vector3D()
    {
    }

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public void Set(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public void Vector3ToVector3F(Vector3 vector)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
    }

    public Vector3 GetAsVector3() => new((float)X, (float)Y, (float)Z);

    public static Vector3D operator +(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public static Vector3D operator -(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    public static Vector3D operator *(Vector3D a, float scalar)
    {
        return new Vector3D(a.X * scalar, a.Y * scalar, a.Z * scalar);
    }

    public static Vector3D operator *(float scalar, Vector3D a)
    {
        return a * scalar;
    }

    public float Length()
    {
        return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    public Vector3D Normalized()
    {
        var length = Length();
        return length > 1e-5f
            ? new Vector3D(X / length, Y / length, Z / length)
            : new Vector3D(0, 0, 0);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
}
