using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector2D
{
    public Vector2D()
    {
    }

    public Vector2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }
    public double Y { get; set; }

    public void Set(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void Vector2ToVector2F(Vector2 vector)
    {
        X = vector.X;
        Y = vector.Y;
    }

    public Vector2 GetAsVector2() => new((float)X, (float)Y);

    public override string ToString()
    {
        return $"({X}, {Y})";
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }
}
