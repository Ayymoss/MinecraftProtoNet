using System;
using MinecraftProtoNet.Physics; // For AxisExtensions (implied)
using MinecraftProtoNet.Enums; // For Axis if needed, or Physics.Axis depending on namespace

namespace MinecraftProtoNet.Physics.Shapes;

public sealed class SubShape : DiscreteVoxelShape
{
    private readonly DiscreteVoxelShape _parent;
    private readonly int _startX;
    private readonly int _startY;
    private readonly int _startZ;
    private readonly int _endX;
    private readonly int _endY;
    private readonly int _endZ;

    public SubShape(DiscreteVoxelShape parent, int startX, int startY, int startZ, int endX, int endY, int endZ)
        : base(endX - startX, endY - startY, endZ - startZ)
    {
        _parent = parent;
        _startX = startX;
        _startY = startY;
        _startZ = startZ;
        _endX = endX;
        _endY = endY;
        _endZ = endZ;
    }

    public override bool IsFull(int x, int y, int z)
    {
        return _parent.IsFull(_startX + x, _startY + y, _startZ + z);
    }

    public override void Fill(int x, int y, int z)
    {
        _parent.Fill(_startX + x, _startY + y, _startZ + z);
    }

    public override int FirstFull(Axis axis)
    {
        return ClampToShape(axis, _parent.FirstFull(axis));
    }

    public override int LastFull(Axis axis)
    {
        return ClampToShape(axis, _parent.LastFull(axis));
    }

    private int ClampToShape(Axis axis, int parentResult)
    {
        int start = axis.Choose(_startX, _startY, _startZ);
        int end = axis.Choose(_endX, _endY, _endZ);
        return Math.Clamp(parentResult, start, end) - start;
    }
}
