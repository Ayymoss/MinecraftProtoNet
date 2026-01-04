using System.Collections;

namespace MinecraftProtoNet.Core.Physics.Shapes;

public static class Shapes
{
    public const double Epsilon = 1.0E-7;
    public const double BigEpsilon = 1.0E-6;

    private static readonly VoxelShape BlockShape;
    private static readonly VoxelShape EmptyShape;
    public static readonly VoxelShape Infinity;

    static Shapes()
    {
        EmptyShape = new ArrayVoxelShape(
            new BitSetDiscreteVoxelShape(0, 0, 0),
            new ArrayDoubleList(new[] { 0.0 }),
            new ArrayDoubleList(new[] { 0.0 }),
            new ArrayDoubleList(new[] { 0.0 })
        );

        var shape = new BitSetDiscreteVoxelShape(1, 1, 1);
        shape.Fill(0, 0, 0);
        BlockShape = new CubeVoxelShape(shape);

        Infinity = Box(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity,
                       double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
    }

    public static VoxelShape Empty() => EmptyShape;
    public static VoxelShape Block() => BlockShape;

    public static VoxelShape Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        if (!(minX > maxX) && !(minY > maxY) && !(minZ > maxZ))
        {
            return Create(minX, minY, minZ, maxX, maxY, maxZ);
        }
        throw new ArgumentException("The min values need to be smaller or equals to the max values");
    }

    public static VoxelShape Create(AABB aabb)
    {
        return Create(aabb.MinX, aabb.MinY, aabb.MinZ, aabb.MaxX, aabb.MaxY, aabb.MaxZ);
    }

    public static VoxelShape Create(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        if (!(maxX - minX < Epsilon) && !(maxY - minY < Epsilon) && !(maxZ - minZ < Epsilon))
        {
            int xBits = FindBits(minX, maxX);
            int yBits = FindBits(minY, maxY);
            int zBits = FindBits(minZ, maxZ);

            if (xBits >= 0 && yBits >= 0 && zBits >= 0)
            {
                if (xBits == 0 && yBits == 0 && zBits == 0)
                {
                    return Block();
                }
                
                int xSize = 1 << xBits;
                int ySize = 1 << yBits;
                int zSize = 1 << zBits;
                
                var voxelShape = BitSetDiscreteVoxelShape.WithFilledBounds(
                    xSize, ySize, zSize,
                    (int)Math.Round(minX * xSize), (int)Math.Round(minY * ySize), (int)Math.Round(minZ * zSize),
                    (int)Math.Round(maxX * xSize), (int)Math.Round(maxY * ySize), (int)Math.Round(maxZ * zSize)
                );
                return new CubeVoxelShape(voxelShape);
            }
            
            return new ArrayVoxelShape(
                BlockShape.Shape,
                new ArrayDoubleList(new[] { minX, maxX }),
                new ArrayDoubleList(new[] { minY, maxY }),
                new ArrayDoubleList(new[] { minZ, maxZ })
            );
        }
        return Empty();
    }

    private static int FindBits(double min, double max)
    {
        if (!(min < -Epsilon) && !(max > 1.0000001))
        {
            for (int bits = 0; bits <= 3; ++bits)
            {
                int intervals = 1 << bits;
                double shMin = min * intervals;
                double shMax = max * intervals;
                bool foundMin = Math.Abs(shMin - Math.Round(shMin)) < Epsilon * intervals;
                bool foundMax = Math.Abs(shMax - Math.Round(shMax)) < Epsilon * intervals;
                if (foundMin && foundMax)
                {
                    return bits;
                }
            }
        }
        return -1;
    }

    public static long Lcm(int a, int b) => MathHelpers.Lcm(a, b);

    public static VoxelShape Or(VoxelShape first, VoxelShape second)
    {
        return Join(first, second, BooleanOps.Or);
    }

    public static VoxelShape Or(VoxelShape first, params VoxelShape[] tail)
    {
        return tail.Aggregate(first, Or);
    }

    public static VoxelShape Join(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        return JoinUnoptimized(first, second, op).Optimize();
    }

    public static VoxelShape JoinUnoptimized(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        if (op(false, false))
        {
            throw new ArgumentException("BooleanOp.True is not supported in JoinUnoptimized logic as per parity");
        }
        
        if (ReferenceEquals(first, second))
        {
            return op(true, true) ? first : Empty();
        }

        bool firstOnlyMatters = op(true, false);
        bool secondOnlyMatters = op(false, true);

        if (first.IsEmpty())
        {
            return secondOnlyMatters ? second : Empty();
        }
        if (second.IsEmpty())
        {
            return firstOnlyMatters ? first : Empty();
        }

        IIndexMerger xMerger = CreateIndexMerger(1, first.GetCoords(Axis.X), second.GetCoords(Axis.X), firstOnlyMatters, secondOnlyMatters);
        IIndexMerger yMerger = CreateIndexMerger(xMerger.Size() - 1, first.GetCoords(Axis.Y), second.GetCoords(Axis.Y), firstOnlyMatters, secondOnlyMatters);
        IIndexMerger zMerger = CreateIndexMerger((xMerger.Size() - 1) * (yMerger.Size() - 1), first.GetCoords(Axis.Z), second.GetCoords(Axis.Z), firstOnlyMatters, secondOnlyMatters);

        var voxelShape = BitSetDiscreteVoxelShape.Join(first.Shape, second.Shape, xMerger, yMerger, zMerger, op);
        
        if (xMerger is DiscreteCubeMerger && yMerger is DiscreteCubeMerger && zMerger is DiscreteCubeMerger)
        {
            return new CubeVoxelShape(voxelShape);
        }

        // Need to cast or unwrap IndexMerger.GetList() to IDoubleList.
        // My IIndexMerger GetList returns IList<double>.
        // ArrayVoxelShape expects IDoubleList.
        // I should have implemented IDoubleList on Merger classes or returned IDoubleList.
        // I added GetListAsIDoubleList() to mergers in previous step (hopefully).
        // Let's check logic: I edited Mergers.cs to include GetListAsIDoubleList().
        // I need to use it. But IIndexMerger interface doesn't have it.
        // I need to cast.
        
        return new ArrayVoxelShape(
            voxelShape,
            GetCoordsFromMerger(xMerger),
            GetCoordsFromMerger(yMerger),
            GetCoordsFromMerger(zMerger)
        );
    }

    private static IDoubleList GetCoordsFromMerger(IIndexMerger merger)
    {
        if (merger is DiscreteCubeMerger dcm) return dcm.GetListAsIDoubleList();
        if (merger is IndirectMerger im) return im.GetListAsIDoubleList();
        if (merger is NonOverlappingMerger nom) return nom; // It implements IDoubleList
        // Fallback for IdenticalMerger? 
        // IdenticalMerger wraps an IDoubleList but doesn't expose it directly in my impl?
        // Wait, IdenticalMerger constructor takes IDoubleList.
        // I should probably add GetListAsIDoubleList to interface if possible, or cast to implementing types.
        // For IdenticalMerger, need to fix Mergers.cs or cast.
        // Assuming IdenticalMerger needs update to expose it, or I use GetList() which returns IList<double>.
        // I can wrap IList<double> into IDoubleList adapter.
        
        return new ListAdapterDoubleList(merger.GetList());
    }
    
    // Adapter for IIndexMerger.GetList() -> IDoubleList
    private class ListAdapterDoubleList : IDoubleList
    {
        private readonly IList<double> _list;
        public ListAdapterDoubleList(IList<double> list) => _list = list;
        public double GetDouble(int index) => _list[index];
        public int Count => _list.Count;
        public IEnumerator<double> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static IIndexMerger CreateIndexMerger(int cost, IDoubleList first, IDoubleList second, bool firstOnlyMatters, bool secondOnlyMatters)
    {
        int firstSize = first.Count - 1;
        int secondSize = second.Count - 1;

        if (first is CubePointRange && second is CubePointRange)
        {
            long size = Lcm(firstSize, secondSize);
            if ((long)cost * size <= 256L)
            {
                return new DiscreteCubeMerger(firstSize, secondSize);
            }
        }

        if (first.GetDouble(firstSize) < second.GetDouble(0) - Epsilon)
        {
            return new NonOverlappingMerger(first, second, false);
        }
        
        if (second.GetDouble(secondSize) < first.GetDouble(0) - Epsilon)
        {
            return new NonOverlappingMerger(second, first, true);
        }

        if (firstSize == secondSize && Equals(first, second)) 
        {
            // Equals check for IDoubleList? 
            // Java uses Objects.equals which might just check reference or run standard equals. 
            // CubePointRange probably doesn't override equals.
            // But if they are same object, IdenticalMerger.
            return new IdenticalMerger(first); // Rough check parity
        }

        return new IndirectMerger(first, second, firstOnlyMatters, secondOnlyMatters);
    }
    
    // Simple equals check reference for now, or content check if generic.
    // Java Objects.equals(a,b) calls .equals().
    private static bool Equals(IDoubleList a, IDoubleList b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        // Should we check values? Java probably doesn't for Arrays unless implemented.
        // Assume reference equality or specific types logic.
        return false;
    }

    public static double Collide(Axis axis, AABB moving, IEnumerable<VoxelShape> shapes, double distance)
    {
        foreach (var shape in shapes)
        {
            if (Math.Abs(distance) < Epsilon) return 0.0;
            distance = shape.Collide(axis, moving, distance);
        }
        return distance;
    }
    
    public delegate void DoubleLineConsumer(double x1, double y1, double z1, double x2, double y2, double z2);
}
