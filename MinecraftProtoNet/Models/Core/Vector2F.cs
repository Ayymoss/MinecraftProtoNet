using System.Numerics;

namespace MinecraftProtoNet.Models.Core;

public class Vector2F
{
    public float X { get; set; }
    public float Y { get; set; }
    
    public void Set(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Vector2ToVector2F(Vector2 vector)
    {
        X = vector.X;
        Y = vector.Y;
    }

    public Vector2 GetAsVector2() => new(X, Y);
}
