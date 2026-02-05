namespace MinecraftProtoNet.Core.Physics.Shapes;

public class ArrayVoxelShape : VoxelShape
{
    private readonly IDoubleList _xs;
    private readonly IDoubleList _ys;
    private readonly IDoubleList _zs;

    public ArrayVoxelShape(DiscreteVoxelShape shape, IDoubleList xs, IDoubleList ys, IDoubleList zs) 
        : base(shape)
    {
        int xSize = shape.GetXSize() + 1;
        int ySize = shape.GetYSize() + 1;
        int zSize = shape.GetZSize() + 1;
        
        if (xSize == xs.Count && ySize == ys.Count && zSize == zs.Count)
        {
            _xs = xs;
            _ys = ys;
            _zs = zs;
        }
        else
        {
            throw new ArgumentException("Lengths of point arrays must be consistent with the size of the VoxelShape.");
        }
    }
    
    // Constructor matching java's public one with arrays if needed, but IDoubleList is more flexible
    public ArrayVoxelShape(DiscreteVoxelShape shape, double[] xs, double[] ys, double[] zs)
        : this(shape, new ArrayDoubleList(xs), new ArrayDoubleList(ys), new ArrayDoubleList(zs))
    {
    }

    public override IDoubleList GetCoords(Axis axis)
    {
        return axis switch
        {
            Axis.X => _xs,
            Axis.Y => _ys,
            Axis.Z => _zs,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }
}
