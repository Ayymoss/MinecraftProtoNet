using System;
using MinecraftProtoNet.Physics;

namespace MinecraftProtoNet.Physics.Shapes;

public sealed class CubeVoxelShape : VoxelShape
{
    public CubeVoxelShape(DiscreteVoxelShape shape) : base(shape)
    {
    }

    public override IDoubleList GetCoords(Axis axis)
    {
        return new CubePointRange(Shape.GetSize(axis));
    }

    private new int FindIndex(Axis axis, double coord)
    {
        int size = Shape.GetSize(axis);
        return (int)Math.Floor(Math.Clamp(coord * size, -1.0, size));
    }
}
