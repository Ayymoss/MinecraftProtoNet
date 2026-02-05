using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.Physics.Shapes;

public abstract class VoxelShape
{
    public readonly DiscreteVoxelShape Shape;
    private VoxelShape[]? _faces;

    protected VoxelShape(DiscreteVoxelShape shape)
    {
        Shape = shape;
    }

    public double Min(Axis axis)
    {
        int i = Shape.FirstFull(axis);
        return i >= Shape.GetSize(axis) ? double.PositiveInfinity : Get(axis, i);
    }

    public double Max(Axis axis)
    {
        int i = Shape.LastFull(axis);
        return i <= 0 ? double.NegativeInfinity : Get(axis, i);
    }

    public AABB Bounds()
    {
        if (IsEmpty())
        {
            throw new InvalidOperationException("No bounds for empty shape.");
        }
        return new AABB(Min(Axis.X), Min(Axis.Y), Min(Axis.Z), Max(Axis.X), Max(Axis.Y), Max(Axis.Z));
    }

    public VoxelShape SingleEncompassing()
    {
        return IsEmpty() ? Shapes.Empty() : Shapes.Box(Min(Axis.X), Min(Axis.Y), Min(Axis.Z), Max(Axis.X), Max(Axis.Y), Max(Axis.Z));
    }

    protected double Get(Axis axis, int i)
    {
        return GetCoords(axis).GetDouble(i);
    }

    public abstract IDoubleList GetCoords(Axis axis);

    public bool IsEmpty()
    {
        return Shape.IsEmpty();
    }

    public VoxelShape Move(double dx, double dy, double dz)
    {
        if (IsEmpty()) return Shapes.Empty();
        return new ArrayVoxelShape(
            Shape, 
            new OffsetDoubleList(GetCoords(Axis.X), dx),
            new OffsetDoubleList(GetCoords(Axis.Y), dy),
            new OffsetDoubleList(GetCoords(Axis.Z), dz)
        );
    }
    
    public VoxelShape Move(Vector3<double> delta) => Move(delta.X, delta.Y, delta.Z);

    public VoxelShape Optimize()
    {
        VoxelShape result = Shapes.Empty();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
        {
            result = Shapes.JoinUnoptimized(result, Shapes.Box(x1, y1, z1, x2, y2, z2), BooleanOps.Or);
        });
        return result;
    }

    public void ForAllEdges(Shapes.DoubleLineConsumer consumer)
    {
        Shape.ForAllEdges((xi1, yi1, zi1, xi2, yi2, zi2) =>
        {
            consumer(
                Get(Axis.X, xi1), Get(Axis.Y, yi1), Get(Axis.Z, zi1),
                Get(Axis.X, xi2), Get(Axis.Y, yi2), Get(Axis.Z, zi2)
            );
        }, true);
    }

    public void ForAllBoxes(Shapes.DoubleLineConsumer consumer)
    {
        IDoubleList xCoords = GetCoords(Axis.X);
        IDoubleList yCoords = GetCoords(Axis.Y);
        IDoubleList zCoords = GetCoords(Axis.Z);
        Shape.ForAllBoxes((xi1, yi1, zi1, xi2, yi2, zi2) =>
        {
            consumer(
                xCoords.GetDouble(xi1), yCoords.GetDouble(yi1), zCoords.GetDouble(zi1),
                xCoords.GetDouble(xi2), yCoords.GetDouble(yi2), zCoords.GetDouble(zi2)
            );
        }, true);
    }
    
    public List<AABB> ToAABBs()
    {
        var list = new List<AABB>();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) => list.Add(new AABB(x1, y1, z1, x2, y2, z2)));
        return list;
    }

    public double Min(Axis axis, double b, double c)
    {
        Axis bAxis = AxisCycle.Forward.Cycle(axis);
        Axis cAxis = AxisCycle.Backward.Cycle(axis);
        int bi = FindIndex(bAxis, b);
        int ci = FindIndex(cAxis, c);
        int i = Shape.FirstFull(axis, bi, ci);
        return i >= Shape.GetSize(axis) ? double.PositiveInfinity : Get(axis, i);
    }

    public double Max(Axis axis, double b, double c)
    {
        Axis bAxis = AxisCycle.Forward.Cycle(axis);
        Axis cAxis = AxisCycle.Backward.Cycle(axis);
        int bi = FindIndex(bAxis, b);
        int ci = FindIndex(cAxis, c);
        int i = Shape.LastFull(axis, bi, ci);
        return i <= 0 ? double.NegativeInfinity : Get(axis, i);
    }

    protected int FindIndex(Axis axis, double coord)
    {
        // Mth.binarySearch equivalent
        // Java: Mth.binarySearch(0, size + 1, index -> coord < get(axis, index)) - 1
        int size = Shape.GetSize(axis);
        int min = 0;
        int max = size + 1;
        while (max > min)
        {
            int mid = min + (max - min) / 2;
            if (coord < Get(axis, mid))
            {
                max = mid;
            }
            else
            {
                min = mid + 1;
            }
        }
        return min - 1;
    }

    public (Vector3<double> Point, BlockFace Face, bool Hit)? Clip(Vector3<double> from, Vector3<double> to, Vector3<int> pos)
    {
         if (IsEmpty()) return null;
         Vector3<double> diff = new(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
         if (diff.LengthSquared() < 1.0E-7) return null;
         
         Vector3<double> testPoint = new(from.X + diff.X * 0.001, from.Y + diff.Y * 0.001, from.Z + diff.Z * 0.001);
         if (Shape.IsFullWide(
             FindIndex(Axis.X, testPoint.X - pos.X),
             FindIndex(Axis.Y, testPoint.Y - pos.Y),
             FindIndex(Axis.Z, testPoint.Z - pos.Z)))
         {
             // Start is inside the shape
             var nearest = Direction.GetApproximateNearest(diff.X, diff.Y, diff.Z).GetOpposite();
             // Map Direction to BlockFace
             // Map logic: West=4, East=5, Down=0, Up=1, North=2, South=3
             // Direction class not available here directly or mapped differently?
             // Using simple mapping based on largest component inverse
             BlockFace face = BlockFace.Top;
             double absX = Math.Abs(diff.X);
             double absY = Math.Abs(diff.Y);
             double absZ = Math.Abs(diff.Z);
             
             if (absX > absY && absX > absZ) face = diff.X > 0 ? BlockFace.West : BlockFace.East;
             else if (absY > absX && absY > absZ) face = diff.Y > 0 ? BlockFace.Bottom : BlockFace.Top; // Hit from bottom if going up? No, hit face is opposite to direction.
             else face = diff.Z > 0 ? BlockFace.North : BlockFace.South;
             
             return (testPoint, face, true);
         }
         
         var aabbs = ToAABBs();
         double minT = double.MaxValue;
         (Vector3<double>? Point, BlockFace Face)? bestHit = null;

         foreach (var aabb in aabbs)
         {
             var offsetAABB = aabb.Move(pos.X, pos.Y, pos.Z);
             var result = AABB.Clip(offsetAABB, from, to);
             if (result.Point != null)
             {
                 double distSq = (result.Point.X - from.X) * (result.Point.X - from.X) + 
                                 (result.Point.Y - from.Y) * (result.Point.Y - from.Y) + 
                                 (result.Point.Z - from.Z) * (result.Point.Z - from.Z);
                 if (distSq < minT)
                 {
                     minT = distSq;
                     bestHit = result;
                 }
             }
         }
         
         if (bestHit != null && bestHit.Value.Point != null)
         {
             return (bestHit.Value.Point, bestHit.Value.Face, true);
         }
         
         return null;
    }

    public VoxelShape GetFaceShape(BlockFace direction)
    {
         if (!IsEmpty() && this != Shapes.Block())
         {
             if (_faces != null)
             {
                 var face = _faces[(int)direction];
                 if (face != null) return face;
             }
             else
             {
                 _faces = new VoxelShape[6];
             }
             
             var calculated = CalculateFace(direction);
             _faces[(int)direction] = calculated;
             return calculated;
         }
         return this;
    }

    private VoxelShape CalculateFace(BlockFace direction)
    {
         // Logic mapping BlockFace to Axis
         // Using extensions or just switch
         Axis axis = direction switch
         {
             BlockFace.West or BlockFace.East => Axis.X,
             BlockFace.Bottom or BlockFace.Top => Axis.Y,
             _ => Axis.Z
         };
         
         if (IsCubeLikeAlong(axis)) return this;
         
         // AxisDirection
         bool positive = direction is BlockFace.East or BlockFace.Top or BlockFace.South;
         
         int index = FindIndex(axis, positive ? 0.9999999 : 1.0E-7);
         SliceShape slice = new SliceShape(this, axis, index);
         if (slice.IsEmpty()) return Shapes.Empty();
         return slice.IsCubeLike() ? Shapes.Block() : slice;
    }

    protected bool IsCubeLike()
    {
        foreach (var axis in AxisExtensions.Values)
        {
            if (!IsCubeLikeAlong(axis)) return false;
        }
        return true;
    }

    private bool IsCubeLikeAlong(Axis axis)
    {
        IDoubleList coords = GetCoords(axis);
        return coords.Count == 2 && 
               Math.Abs(coords.GetDouble(0) - 0.0) < 1.0E-7 && 
               Math.Abs(coords.GetDouble(1) - 1.0) < 1.0E-7;
    }

    public double Collide(Axis axis, AABB moving, double distance)
    {
        return CollideX(AxisCycle.Between(axis, Axis.X), moving, distance);
    }

    protected double CollideX(AxisCycle transform, AABB moving, double distance)
    {
        if (IsEmpty()) return distance;
        if (Math.Abs(distance) < 1.0E-7) return 0.0;
        
        AxisCycle inverse = transform.Inverse();
        Axis aAxis = inverse.Cycle(Axis.X);
        Axis bAxis = inverse.Cycle(Axis.Y);
        Axis cAxis = inverse.Cycle(Axis.Z);
        
        double maxA = moving.GetMax(aAxis);
        double minA = moving.GetMin(aAxis);
        
        int aMin = FindIndex(aAxis, minA + 1.0E-7);
        int aMax = FindIndex(aAxis, maxA - 1.0E-7);
        
        int bMin = Math.Max(0, FindIndex(bAxis, moving.GetMin(bAxis) + 1.0E-7));
        int bMax = Math.Min(Shape.GetSize(bAxis), FindIndex(bAxis, moving.GetMax(bAxis) - 1.0E-7) + 1);
        
        int cMin = Math.Max(0, FindIndex(cAxis, moving.GetMin(cAxis) + 1.0E-7));
        int cMax = Math.Min(Shape.GetSize(cAxis), FindIndex(cAxis, moving.GetMax(cAxis) - 1.0E-7) + 1);
        
        int aSize = Shape.GetSize(aAxis);
        
        if (distance > 0.0)
        {
            for (int a = aMax + 1; a < aSize; ++a)
            {
                for (int b = bMin; b < bMax; ++b)
                {
                    for (int c = cMin; c < cMax; ++c)
                    {
                        if (Shape.IsFullWide(inverse, a, b, c))
                        {
                            double newDistance = Get(aAxis, a) - maxA;
                            if (newDistance >= -1.0E-7)
                            {
                                distance = Math.Min(distance, newDistance);
                            }
                            return distance;
                        }
                    }
                }
            }
        }
        else if (distance < 0.0)
        {
            for (int a = aMin - 1; a >= 0; --a)
            {
                for (int b = bMin; b < bMax; ++b)
                {
                    for (int c = cMin; c < cMax; ++c)
                    {
                        if (Shape.IsFullWide(inverse, a, b, c))
                        {
                            double newDistance = Get(aAxis, a + 1) - minA;
                            if (newDistance <= 1.0E-7)
                            {
                                distance = Math.Max(distance, newDistance);
                            }
                            return distance;
                        }
                    }
                }
            }
        }
        return distance;
    }
}
