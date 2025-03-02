using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector3F
{
    public Vector3F()
    {
    }

    public Vector3F(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public void Set(float x, float y, float z)
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

    public Vector3 GetAsVector3() => new(X, Y, Z);

    // Operator overloading for Vector3F + Vector3F
    public static Vector3F operator +(Vector3F a, Vector3F b)
    {
        return new Vector3F(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    // Optional: Operator overloading for Vector3F - Vector3F (Subtraction, might be useful)
    public static Vector3F operator -(Vector3F a, Vector3F b)
    {
        return new Vector3F(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    // Optional: Operator overloading for Vector3F * float (Scalar Multiplication)
    public static Vector3F operator *(Vector3F a, float scalar)
    {
        return new Vector3F(a.X * scalar, a.Y * scalar, a.Z * scalar);
    }

    // Optional: Operator overloading for float * Vector3F (Scalar Multiplication - commutative)
    public static Vector3F operator *(float scalar, Vector3F a)
    {
        return a * scalar; // Reuse the previous operator*
    }

    // Optional: Method to calculate the length (magnitude) of the vector
    public float Length()
    {
        return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    // Optional: Method to normalize the vector (make it have length 1)
    public Vector3F Normalized()
    {
        var length = Length();
        return length > 1e-5f
            ? new Vector3F(X / length, Y / length, Z / length) // Avoid division by zero
            : new Vector3F(0, 0, 0); // Or return the zero vector if length is very small
    }
}
