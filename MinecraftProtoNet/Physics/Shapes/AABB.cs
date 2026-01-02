using System;
using System.Collections.Generic;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Enums;

namespace MinecraftProtoNet.Physics.Shapes;

/// <summary>
/// Axis Aligned Bounding Box.
/// Parity with net.minecraft.world.phys.AABB
/// </summary>
public readonly struct AABB(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    : IEquatable<AABB>
{
    private const double Epsilon = 1.0E-7;
    public readonly double MinX = Math.Min(minX, maxX);
    public readonly double MinY = Math.Min(minY, maxY);
    public readonly double MinZ = Math.Min(minZ, maxZ);
    public readonly double MaxX = Math.Max(minX, maxX);
    public readonly double MaxY = Math.Max(minY, maxY);
    public readonly double MaxZ = Math.Max(minZ, maxZ);

    public AABB(Vector3<double> min, Vector3<double> max) 
        : this(min.X, min.Y, min.Z, max.X, max.Y, max.Z) { }
        
    public Vector3<double> Min => new(MinX, MinY, MinZ);
    public Vector3<double> Max => new(MaxX, MaxY, MaxZ);

    public double GetMin(Axis axis) => axis switch
    {
        Axis.X => MinX,
        Axis.Y => MinY,
        Axis.Z => MinZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
    };

    public double GetMax(Axis axis) => axis switch
    {
        Axis.X => MaxX,
        Axis.Y => MaxY,
        Axis.Z => MaxZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
    };
    
    public AABB Expand(double x, double y, double z) => ExpandTowards(x, y, z);
    public AABB Expand(double value) => ExpandTowards(value, value, value);
    public AABB Expand(Vector3<double> delta) => ExpandTowards(delta);
        
    public static AABB Of(double x1, double y1, double z1, double x2, double y2, double z2)
    {
        return new AABB(x1, y1, z1, x2, y2, z2);
    }

    public AABB Contract(double x, double y, double z)
    {
        double minX = MinX;
        double minY = MinY;
        double minZ = MinZ;
        double maxX = MaxX;
        double maxY = MaxY;
        double maxZ = MaxZ;

        if (x < 0) minX -= x;
        else if (x > 0) maxX -= x;
        
        if (y < 0) minY -= y;
        else if (y > 0) maxY -= y;
        
        if (z < 0) minZ -= z;
        else if (z > 0) maxZ -= z;

        return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public AABB ExpandTowards(double x, double y, double z)
    {
        double minX = MinX;
        double minY = MinY;
        double minZ = MinZ;
        double maxX = MaxX;
        double maxY = MaxY;
        double maxZ = MaxZ;

        if (x < 0) minX += x;
        else if (x > 0) maxX += x;
        
        if (y < 0) minY += y;
        else if (y > 0) maxY += y;
        
        if (z < 0) minZ += z;
        else if (z > 0) maxZ += z;

        return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
    }
    
    public AABB ExpandTowards(Vector3<double> delta) => ExpandTowards(delta.X, delta.Y, delta.Z);

    public AABB Inflate(double x, double y, double z)
    {
        return new AABB(MinX - x, MinY - y, MinZ - z, MaxX + x, MaxY + y, MaxZ + z);
    }
    
    public AABB Inflate(double value) => Inflate(value, value, value);
    
    public AABB Deflate(double x, double y, double z) => Inflate(-x, -y, -z);
    
    public AABB Deflate(double value) => Inflate(-value);

    public AABB Intersect(AABB other)
    {
        return new AABB(
            Math.Max(MinX, other.MinX),
            Math.Max(MinY, other.MinY),
            Math.Max(MinZ, other.MinZ),
            Math.Min(MaxX, other.MaxX),
            Math.Min(MaxY, other.MaxY),
            Math.Min(MaxZ, other.MaxZ));
    }
    
    public AABB Union(AABB other)
    {
        return new AABB(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY),
            Math.Max(MaxZ, other.MaxZ));
    }

    public AABB Move(double x, double y, double z)
    {
        return new AABB(MinX + x, MinY + y, MinZ + z, MaxX + x, MaxY + y, MaxZ + z);
    }
    
    public AABB Move(Vector3<double> offset) => Move(offset.X, offset.Y, offset.Z);
    
    // Alias for compatibility with legacy usage
    public AABB Offset(double x, double y, double z) => Move(x, y, z);
    public AABB Offset(Vector3<double> offset) => Move(offset);

    public bool Intersects(AABB other)
    {
        return Intersects(other.MinX, other.MinY, other.MinZ, other.MaxX, other.MaxY, other.MaxZ);
    }

    public bool Intersects(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        return MinX < maxX && MaxX > minX && MinY < maxY && MaxY > minY && MinZ < maxZ && MaxZ > minZ;
    }

    public bool Contains(double x, double y, double z)
    {
        return x >= MinX && x < MaxX && y >= MinY && y < MaxY && z >= MinZ && z < MaxZ;
    }
    
    public bool Contains(Vector3<double> point) => Contains(point.X, point.Y, point.Z);

    public double GetSizeX() => MaxX - MinX;
    public double GetSizeY() => MaxY - MinY;
    public double GetSizeZ() => MaxZ - MinZ;

    public Vector3<double> GetCenter()
    {
        return new Vector3<double>((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5, (MinZ + MaxZ) * 0.5);
    }
    
    public double CalculateXOffset(AABB other, double offsetX)
    {
        if (other.MaxY > MinY && other.MinY < MaxY && other.MaxZ > MinZ && other.MinZ < MaxZ)
        {
            if (offsetX > 0.0 && other.MaxX <= MinX)
            {
                double d = MinX - other.MaxX;
                if (d < offsetX) offsetX = d;
            }
            else if (offsetX < 0.0 && other.MinX >= MaxX)
            {
                double d = MaxX - other.MinX;
                if (d > offsetX) offsetX = d;
            }
        }
        return offsetX;
    }
    
    public double CalculateYOffset(AABB other, double offsetY)
    {
        if (other.MaxX > MinX && other.MinX < MaxX && other.MaxZ > MinZ && other.MinZ < MaxZ)
        {
            if (offsetY > 0.0 && other.MaxY <= MinY)
            {
                double d = MinY - other.MaxY;
                if (d < offsetY) offsetY = d;
            }
            else if (offsetY < 0.0 && other.MinY >= MaxY)
            {
                double d = MaxY - other.MinY;
                if (d > offsetY) offsetY = d;
            }
        }
        return offsetY;
    }
    
    public double CalculateZOffset(AABB other, double offsetZ)
    {
        if (other.MaxX > MinX && other.MinX < MaxX && other.MaxY > MinY && other.MinY < MaxY)
        {
            if (offsetZ > 0.0 && other.MaxZ <= MinZ)
            {
                double d = MinZ - other.MaxZ;
                if (d < offsetZ) offsetZ = d;
            }
            else if (offsetZ < 0.0 && other.MinZ >= MaxZ)
            {
                double d = MaxZ - other.MinZ;
                if (d > offsetZ) offsetZ = d;
            }
        }
        return offsetZ;
    }

    public static (Vector3<double>? Point, BlockFace Face) Clip(AABB aabb, Vector3<double> start, Vector3<double> end)
    {
        Vector3<double> direction = new(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        double minT = double.MaxValue;
        BlockFace? face = null;
        
        // Helper to check containment within face bounds
        bool IsInYZ(double x)
        {
             double t = (x - start.X) / direction.X;
             if (t < 0 || t > 1) return false;
             double y = start.Y + direction.Y * t;
             double z = start.Z + direction.Z * t;
             return y >= aabb.MinY - Epsilon && y <= aabb.MaxY + Epsilon && z >= aabb.MinZ - Epsilon && z <= aabb.MaxZ + Epsilon;
        }
        
        bool IsInXZ(double y)
        {
             double t = (y - start.Y) / direction.Y;
             if (t < 0 || t > 1) return false;
             double x = start.X + direction.X * t;
             double z = start.Z + direction.Z * t;
             return x >= aabb.MinX - Epsilon && x <= aabb.MaxX + Epsilon && z >= aabb.MinZ - Epsilon && z <= aabb.MaxZ + Epsilon;
        }
        
        bool IsInXY(double z)
        {
             double t = (z - start.Z) / direction.Z;
             if (t < 0 || t > 1) return false;
             double x = start.X + direction.X * t;
             double y = start.Y + direction.Y * t;
             return x >= aabb.MinX - Epsilon && x <= aabb.MaxX + Epsilon && y >= aabb.MinY - Epsilon && y <= aabb.MaxY + Epsilon;
        }

        // Check each face
        if (Math.Abs(direction.X) > Epsilon)
        {
             // West (-X)
             if (IsInYZ(aabb.MinX))
             {
                 double t = (aabb.MinX - start.X) / direction.X;
                 if (t < minT) { minT = t; face = BlockFace.West; }
             }
             // East (+X)
             if (IsInYZ(aabb.MaxX))
             {
                 double t = (aabb.MaxX - start.X) / direction.X;
                 if (t < minT) { minT = t; face = BlockFace.East; }
             }
        }
        
        if (Math.Abs(direction.Y) > Epsilon)
        {
             // Bottom (-Y)
             if (IsInXZ(aabb.MinY))
             {
                 double t = (aabb.MinY - start.Y) / direction.Y;
                 if (t < minT) { minT = t; face = BlockFace.Bottom; }
             }
             // Top (+Y)
             if (IsInXZ(aabb.MaxY))
             {
                 double t = (aabb.MaxY - start.Y) / direction.Y;
                 if (t < minT) { minT = t; face = BlockFace.Top; }
             }
        }
        
        if (Math.Abs(direction.Z) > Epsilon)
        {
             // North (-Z)
             if (IsInXY(aabb.MinZ))
             {
                 double t = (aabb.MinZ - start.Z) / direction.Z;
                 if (t < minT) { minT = t; face = BlockFace.North; }
             }
             // South (+Z)
             if (IsInXY(aabb.MaxZ))
             {
                 double t = (aabb.MaxZ - start.Z) / direction.Z;
                 if (t < minT) { minT = t; face = BlockFace.South; }
             }
        }

        if (face == null) return (null, BlockFace.Top);
        
        return (new Vector3<double>(
            start.X + direction.X * minT,
            start.Y + direction.Y * minT, 
            start.Z + direction.Z * minT), face.Value);
    }

    public override string ToString() => $"AABB({MinX:F3}, {MinY:F3}, {MinZ:F3} -> {MaxX:F3}, {MaxY:F3}, {MaxZ:F3})";
    
    public bool Equals(AABB other)
    {
        return Math.Abs(MinX - other.MinX) < Epsilon && 
               Math.Abs(MinY - other.MinY) < Epsilon && 
               Math.Abs(MinZ - other.MinZ) < Epsilon && 
               Math.Abs(MaxX - other.MaxX) < Epsilon && 
               Math.Abs(MaxY - other.MaxY) < Epsilon && 
               Math.Abs(MaxZ - other.MaxZ) < Epsilon;
    }

    public override bool Equals(object? obj) => obj is AABB other && Equals(other);
    
    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);

    public static bool operator ==(AABB left, AABB right) => left.Equals(right);
    public static bool operator !=(AABB left, AABB right) => !left.Equals(right);
}
