namespace MinecraftProtoNet.Core.Physics.Shapes;

public class SliceShape : VoxelShape
{
    private readonly VoxelShape _delegateShape;
    private readonly Axis _axis;
    private static readonly IDoubleList SliceCoords = new CubePointRange(1);

    public SliceShape(VoxelShape delegateShape, Axis axis, int point)
        : base(MakeSlice(delegateShape.Shape, axis, point))
    {
        _delegateShape = delegateShape;
        _axis = axis;
    }

    private static DiscreteVoxelShape MakeSlice(DiscreteVoxelShape delegateShape, Axis axis, int point)
    {
        return new SubShape(
            delegateShape,
            axis.Choose(point, 0, 0),
            axis.Choose(0, point, 0),
            axis.Choose(0, 0, point),
            axis.Choose(point + 1, delegateShape.GetXSize(), delegateShape.GetXSize()),
            axis.Choose(delegateShape.GetYSize(), point + 1, delegateShape.GetYSize()),
            axis.Choose(delegateShape.GetZSize(), delegateShape.GetZSize(), point + 1)
        );
    }

    public override IDoubleList GetCoords(Axis axis)
    {
        return axis == _axis ? SliceCoords : _delegateShape.GetCoords(axis);
    }
    
    public new bool IsCubeLike()
    {
        // Actually SliceShape in Java doesn't override IsCubeLike?
        // Wait, VoxelShape has IsCubeLike() as protected.
        // SliceShape usage in VoxelShape.CalculateFace casts it to (VoxelShape) and checks slice.isCubeLike().
        // So base IsCubeLike works.
        // But since this is a different class, I should verify inheritance.
        // base.IsCubeLike() iterates coords. GetCoords is overridden. So it works.
        // But VoxelShape.CalculateFace calls slice.IsCubeLike? 
        // Yes, VoxelShape.cs calls IsCubeLike() on the result.
        // VoxelShape IsCubeLike calls GetCoords. 
        // Correct.
        return base.IsCubeLike(); 
        // No need to redeclare unless visibility issue. 
        // VoxelShape.IsCubeLike is protected in Java, but I made it protected in C#.
        // But CalculateFace is private inside VoxelShape, so it can call protected members of SAME class type?
        // In Java yes. In C# only if "slice" is seen as "VoxelShape".
        // Accessing protected member on *another instance* is only allowed if it's the same class or subclass accessing base.
        // In VoxelShape.cs: SliceShape slice = ... slice.IsCubeLike() 
        // This fails if IsCubeLike is protected and accessed from VoxelShape on SliceShape instance?
        // Actually in C# `protected` access is strict.
        // VoxelShape can access `this.IsCubeLike`.
        // VoxelShape cannot access `slice.IsCubeLike` unless `slice` is treated as `this` context? No.
        // I should probably make `IsCubeLike` internal or public to make life easier in the library.
        // Or cast logic.
        // I'll leave it protected and see if I need to expose it.
        // Actually, I'll make it internal in VoxelShape.cs (implied by lack of access modifier change if I didn't specify).
        // I made it protected in VoxelShape.cs.
        // I will change it to `internal protected` or just `public` later if needed, or rely on internal access.
        // For now, assume strict C# might complain.
        // I'll add `internal bool IsCubeLikeInternal => IsCubeLike();` bridge if needed.
        // Or just make it public. VoxelShape logic is complex enough.
    }
}
