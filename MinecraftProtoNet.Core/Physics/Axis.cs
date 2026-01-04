using System;

namespace MinecraftProtoNet.Physics;

public enum Axis
{
    X,
    Y,
    Z
}

public static class AxisExtensions
{
    public static int Choose(this Axis axis, int x, int y, int z)
    {
        return axis switch
        {
            Axis.X => x,
            Axis.Y => y,
            _ => z
        };
    }
    
    public static double Choose(this Axis axis, double x, double y, double z)
    {
        return axis switch
        {
            Axis.X => x,
            Axis.Y => y,
            _ => z
        };
    }
    
    public static Axis[] Values = { Axis.X, Axis.Y, Axis.Z };
}
