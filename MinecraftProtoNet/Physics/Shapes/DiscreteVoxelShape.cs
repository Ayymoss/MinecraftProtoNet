using System;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core; // For Vector3<T> if needed, logic uses ints

namespace MinecraftProtoNet.Physics.Shapes;

public abstract class DiscreteVoxelShape
{
    private static readonly Axis[] AxisValues = { Axis.X, Axis.Y, Axis.Z };
    
    protected readonly int XSize;
    protected readonly int YSize;
    protected readonly int ZSize;

    protected DiscreteVoxelShape(int xSize, int ySize, int zSize)
    {
        if (xSize < 0 || ySize < 0 || zSize < 0)
        {
            throw new ArgumentException($"Need all positive sizes: x: {xSize}, y: {ySize}, z: {zSize}");
        }
        XSize = xSize;
        YSize = ySize;
        ZSize = zSize;
    }

    public bool IsFullWide(AxisCycle transform, int x, int y, int z)
    {
        return IsFullWide(
            transform.Cycle(x, y, z, Axis.X),
            transform.Cycle(x, y, z, Axis.Y),
            transform.Cycle(x, y, z, Axis.Z));
    }

    public bool IsFullWide(int x, int y, int z)
    {
        if (x >= 0 && y >= 0 && z >= 0)
        {
            return (x < XSize && y < YSize && z < ZSize) && IsFull(x, y, z);
        }
        return false;
    }

    public bool IsFull(AxisCycle transform, int x, int y, int z)
    {
        return IsFull(
            transform.Cycle(x, y, z, Axis.X),
            transform.Cycle(x, y, z, Axis.Y),
            transform.Cycle(x, y, z, Axis.Z));
    }

    public abstract bool IsFull(int x, int y, int z);
    public abstract void Fill(int x, int y, int z);

    public bool IsEmpty()
    {
        foreach (var axis in AxisValues)
        {
            if (FirstFull(axis) >= LastFull(axis))
            {
                return true;
            }
        }
        return false;
    }

    public abstract int FirstFull(Axis axis);
    public abstract int LastFull(Axis axis);

    public int FirstFull(Axis aAxis, int b, int c)
    {
        int aSize = GetSize(aAxis);
        if (b >= 0 && c >= 0)
        {
            Axis bAxis = AxisCycle.Forward.Cycle(aAxis);
            Axis cAxis = AxisCycle.Backward.Cycle(aAxis);
            if (b < GetSize(bAxis) && c < GetSize(cAxis))
            {
                AxisCycle transform = AxisCycle.Between(Axis.X, aAxis);
                for (int a = 0; a < aSize; ++a)
                {
                    if (IsFull(transform, a, b, c))
                    {
                        return a;
                    }
                }
                return aSize;
            }
        }
        return aSize;
    }

    public int LastFull(Axis aAxis, int b, int c)
    {
        if (b >= 0 && c >= 0)
        {
            Axis bAxis = AxisCycle.Forward.Cycle(aAxis);
            Axis cAxis = AxisCycle.Backward.Cycle(aAxis);
            if (b < GetSize(bAxis) && c < GetSize(cAxis))
            {
                int aSize = GetSize(aAxis);
                AxisCycle transform = AxisCycle.Between(Axis.X, aAxis);
                for (int a = aSize - 1; a >= 0; --a)
                {
                    if (IsFull(transform, a, b, c))
                    {
                        return a + 1;
                    }
                }
                return 0;
            }
        }
        return 0;
    }

    public int GetSize(Axis axis) => axis.Choose(XSize, YSize, ZSize);

    public int GetXSize() => GetSize(Axis.X);
    public int GetYSize() => GetSize(Axis.Y);
    public int GetZSize() => GetSize(Axis.Z);

    public void ForAllEdges(IntLineConsumer consumer, bool mergeNeighbors)
    {
        ForAllAxisEdges(consumer, AxisCycle.None, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Forward, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Backward, mergeNeighbors);
    }

    private void ForAllAxisEdges(IntLineConsumer consumer, AxisCycle transform, bool mergeNeighbors)
    {
        AxisCycle inverse = transform.Inverse();
        int aSize = GetSize(inverse.Cycle(Axis.X));
        int bSize = GetSize(inverse.Cycle(Axis.Y));
        int cSize = GetSize(inverse.Cycle(Axis.Z));

        for (int a = 0; a <= aSize; ++a)
        {
            for (int b = 0; b <= bSize; ++b)
            {
                int lastStart = -1;
                for (int c = 0; c <= cSize; ++c)
                {
                    int fullSectors = 0;
                    int oddSectors = 0;

                    for (int da = 0; da <= 1; ++da)
                    {
                        for (int db = 0; db <= 1; ++db)
                        {
                            if (IsFullWide(inverse, a + da - 1, b + db - 1, c))
                            {
                                ++fullSectors;
                                oddSectors ^= da ^ db;
                            }
                        }
                    }

                    if (fullSectors == 1 || fullSectors == 3 || (fullSectors == 2 && (oddSectors & 1) == 0))
                    {
                        if (mergeNeighbors)
                        {
                            if (lastStart == -1)
                            {
                                lastStart = c;
                            }
                        }
                        else
                        {
                            consumer(
                                inverse.Cycle(a, b, c, Axis.X),
                                inverse.Cycle(a, b, c, Axis.Y),
                                inverse.Cycle(a, b, c, Axis.Z),
                                inverse.Cycle(a, b, c + 1, Axis.X),
                                inverse.Cycle(a, b, c + 1, Axis.Y),
                                inverse.Cycle(a, b, c + 1, Axis.Z));
                        }
                    }
                    else if (lastStart != -1)
                    {
                        consumer(
                            inverse.Cycle(a, b, lastStart, Axis.X),
                            inverse.Cycle(a, b, lastStart, Axis.Y),
                            inverse.Cycle(a, b, lastStart, Axis.Z),
                            inverse.Cycle(a, b, c, Axis.X),
                            inverse.Cycle(a, b, c, Axis.Y),
                            inverse.Cycle(a, b, c, Axis.Z));
                        lastStart = -1;
                    }
                }
            }
        }
    }
    
    public void ForAllBoxes(IntLineConsumer consumer, bool mergeNeighbors)
    {
        BitSetDiscreteVoxelShape.ForAllBoxes(this, consumer, mergeNeighbors);
    }

    public delegate void IntFaceConsumer(BlockFace direction, int x, int y, int z);
    public delegate void IntLineConsumer(int x1, int y1, int z1, int x2, int y2, int z2);
    
    // Missing: ForAllFaces implementation if needed later, but ForAllBoxes is main one for AABB conversion
}
