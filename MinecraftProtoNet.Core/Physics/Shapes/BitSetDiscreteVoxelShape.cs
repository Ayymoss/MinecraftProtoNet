using System;
using System.Collections;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Physics;

namespace MinecraftProtoNet.Physics.Shapes;

public sealed class BitSetDiscreteVoxelShape : DiscreteVoxelShape
{
    private readonly BitArray _storage;
    private int _xMin;
    private int _yMin;
    private int _zMin;
    private int _xMax;
    private int _yMax;
    private int _zMax;

    public BitSetDiscreteVoxelShape(int xSize, int ySize, int zSize) 
        : base(xSize, ySize, zSize)
    {
        _storage = new BitArray(xSize * ySize * zSize);
        _xMin = xSize;
        _yMin = ySize;
        _zMin = zSize;
        _xMax = 0; // Java init uses 0 in default bounds? Logic in constructor sets to size? 
        // Java: this.xMin = xSize; ... this.xMax = xSize; is WRONG in my reading?
        // Reading java again: 
        // this.xMin = xSize; ...
        // this.xMax = 0; is NOT in java code shown?
        // Java code:
        // this.xMin = xSize;
        // this.yMin = ySize;
        // this.zMin = zSize;
        // Fields xMax/yMax/zMax are int default (0).
        // So yes, starts inverted for min, and 0 for max.
        _xMax = 0;
        _yMax = 0;
        _zMax = 0;
    }

    public BitSetDiscreteVoxelShape(DiscreteVoxelShape other) 
        : base(other.GetXSize(), other.GetYSize(), other.GetZSize())
    {
        if (other is BitSetDiscreteVoxelShape bitSetShape)
        {
            _storage = (BitArray)bitSetShape._storage.Clone();
        }
        else
        {
            _storage = new BitArray(XSize * YSize * ZSize);
            for (int x = 0; x < XSize; ++x)
            {
                for (int y = 0; y < YSize; ++y)
                {
                    for (int z = 0; z < ZSize; ++z)
                    {
                        if (other.IsFull(x, y, z))
                        {
                             _storage.Set(GetIndex(x, y, z), true);
                        }
                    }
                }
            }
        }
        
        _xMin = other.FirstFull(Axis.X);
        _yMin = other.FirstFull(Axis.Y);
        _zMin = other.FirstFull(Axis.Z);
        _xMax = other.LastFull(Axis.X);
        _yMax = other.LastFull(Axis.Y);
        _zMax = other.LastFull(Axis.Z);
    }
    
    // Static factory for filled partial bounds
    public static BitSetDiscreteVoxelShape WithFilledBounds(int xSize, int ySize, int zSize, int xMin, int yMin, int zMin, int xMax, int yMax, int zMax)
    {
        var shape = new BitSetDiscreteVoxelShape(xSize, ySize, zSize);
        shape._xMin = xMin;
        shape._yMin = yMin;
        shape._zMin = zMin;
        shape._xMax = xMax;
        shape._yMax = yMax;
        shape._zMax = zMax;
        
        for (int x = xMin; x < xMax; ++x)
        {
            for (int y = yMin; y < yMax; ++y)
            {
                for (int z = zMin; z < zMax; ++z)
                {
                    shape.FillUpdateBounds(x, y, z, false);
                }
            }
        }
        return shape;
    }

    private int GetIndex(int x, int y, int z)
    {
        return (x * YSize + y) * ZSize + z;
    }

    public override bool IsFull(int x, int y, int z)
    {
        return _storage.Get(GetIndex(x, y, z));
    }

    private void FillUpdateBounds(int x, int y, int z, bool updateBounds)
    {
        _storage.Set(GetIndex(x, y, z), true);
        if (updateBounds)
        {
            _xMin = Math.Min(_xMin, x);
            _yMin = Math.Min(_yMin, y);
            _zMin = Math.Min(_zMin, z);
            _xMax = Math.Max(_xMax, x + 1);
            _yMax = Math.Max(_yMax, y + 1);
            _zMax = Math.Max(_zMax, z + 1);
        }
    }

    public override void Fill(int x, int y, int z)
    {
        FillUpdateBounds(x, y, z, true);
    }

    public override int FirstFull(Axis axis)
    {
        return axis.Choose(_xMin, _yMin, _zMin);
    }

    public override int LastFull(Axis axis)
    {
        return axis.Choose(_xMax, _yMax, _zMax);
    }

    public static BitSetDiscreteVoxelShape Join(DiscreteVoxelShape first, DiscreteVoxelShape second, IIndexMerger xMerger, IIndexMerger yMerger, IIndexMerger zMerger, BooleanOp op)
    {
        var shape = new BitSetDiscreteVoxelShape(xMerger.Size() - 1, yMerger.Size() - 1, zMerger.Size() - 1);
        int[] bounds = { int.MaxValue, int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, int.MinValue }; // minX, minY, minZ, maxX, maxY, maxZ
        
        xMerger.ForMergedIndexes((x1, x2, xr) =>
        {
            bool updatedSlice = false;
            yMerger.ForMergedIndexes((y1, y2, yr) =>
            {
                bool updatedColumn = false;
                zMerger.ForMergedIndexes((z1, z2, zr) =>
                {
                    if (op(first.IsFullWide(x1, y1, z1), second.IsFullWide(x2, y2, z2)))
                    {
                        shape._storage.Set(shape.GetIndex(xr, yr, zr), true);
                        bounds[2] = Math.Min(bounds[2], zr);
                        bounds[5] = Math.Max(bounds[5], zr);
                        updatedColumn = true;
                    }
                    return true;
                });
                
                if (updatedColumn)
                {
                    bounds[1] = Math.Min(bounds[1], yr);
                    bounds[4] = Math.Max(bounds[4], yr);
                    updatedSlice = true;
                }
                return true;
            });
            
            if (updatedSlice)
            {
                bounds[0] = Math.Min(bounds[0], xr);
                bounds[3] = Math.Max(bounds[3], xr);
            }
            return true;
        });
        
        // Only update bounds if we found something (default logic in Java sets these if touched)
        // Java code sets them directly. If nothing touched, they remain MaxValue/MinValue.
        // We probably should check if empty? Java doesn't explicitly check emptiness before setting.
        // But if empty, bounds remain invalid values?
        // Actually java initializes bounds to these values.
        
        shape._xMin = bounds[0];
        shape._yMin = bounds[1];
        shape._zMin = bounds[2];
        shape._xMax = bounds[3] + 1;
        shape._yMax = bounds[4] + 1;
        shape._zMax = bounds[5] + 1;
        
        // Correction for empty shapes? Java doesn't show it in snippet.
        // If nothing matches, Min will be MaxValue.
        // We should clamp or reset if empty? 
        // Let's stick to parity.
        
        return shape;
    }

    public static void ForAllBoxes(DiscreteVoxelShape voxelShape, IntLineConsumer consumer, bool mergeNeighbors)
    {
        var shape = new BitSetDiscreteVoxelShape(voxelShape);
        
        for (int y = 0; y < shape.YSize; ++y)
        {
            for (int x = 0; x < shape.XSize; ++x)
            {
                int lastStartZ = -1;
                for (int z = 0; z <= shape.ZSize; ++z)
                {
                    if (shape.IsFullWide(x, y, z))
                    {
                        if (mergeNeighbors)
                        {
                            if (lastStartZ == -1) lastStartZ = z;
                        }
                        else
                        {
                            consumer(x, y, z, x + 1, y + 1, z + 1);
                        }
                    }
                    else if (lastStartZ != -1)
                    {
                        int endX = x;
                        int endY = y;
                        shape.ClearZStrip(lastStartZ, z, x, y);
                        
                        while (shape.IsZStripFull(lastStartZ, z, endX + 1, y))
                        {
                            shape.ClearZStrip(lastStartZ, z, endX + 1, y);
                            ++endX;
                        }
                        
                        while (shape.IsXZRectangleFull(x, endX + 1, lastStartZ, z, endY + 1))
                        {
                            for (int cx = x; cx <= endX; ++cx)
                            {
                                shape.ClearZStrip(lastStartZ, z, cx, endY + 1);
                            }
                            ++endY;
                        }
                        
                        consumer(x, y, lastStartZ, endX + 1, endY + 1, z);
                        lastStartZ = -1;
                    }
                }
            }
        }
    }

    private bool IsZStripFull(int startZ, int endZ, int x, int y)
    {
        if (x < XSize && y < YSize)
        {
            return NextClearBit(GetIndex(x, y, startZ)) >= GetIndex(x, y, endZ);
        }
        return false;
    }
    
    private int NextClearBit(int fromIndex)
    {
        int len = _storage.Length;
        for (int i = fromIndex; i < len; i++)
        {
            if (!_storage.Get(i)) return i;
        }
        return len;
    }

    private bool IsXZRectangleFull(int startX, int endX, int startZ, int endZ, int y)
    {
        for (int x = startX; x < endX; ++x)
        {
            if (!IsZStripFull(startZ, endZ, x, y))
            {
                return false;
            }
        }
        return true;
    }

    private void ClearZStrip(int startZ, int endZ, int x, int y)
    {
        int start = GetIndex(x, y, startZ);
        int end = GetIndex(x, y, endZ);
        for (int i = start; i < end; i++)
        {
             _storage.Set(i, false);
        }
    }
}
