namespace MinecraftProtoNet.Models.Core;

// ReSharper disable once InconsistentNaming
public struct AABB
{
    public Vector3<double> Min { get; private set; }
    public Vector3<double> Max { get; private set; }

    public AABB(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        Min = new Vector3<double>(minX, minY, minZ);
        Max = new Vector3<double>(maxX, maxY, maxZ);
    }

    public AABB(Vector3<double> min, Vector3<double> max)
    {
        Min = min;
        Max = max;
    }

    public double SizeX => Max.X - Min.X;
    public double SizeY => Max.Y - Min.Y;
    public double SizeZ => Max.Z - Min.Z;

    public AABB Expand(double value)
    {
        return Expand(value, value, value);
    }

    public AABB Expand(double dx, double dy, double dz)
    {
        return new AABB(
            Min.X - dx, Min.Y - dy, Min.Z - dz,
            Max.X + dx, Max.Y + dy, Max.Z + dz
        );
    }

    public AABB Offset(double dx, double dy, double dz)
    {
        return new AABB(Min.X + dx, Min.Y + dy, Min.Z + dz, Max.X + dx, Max.Y + dy, Max.Z + dz);
    }

    public AABB Offset(Vector3<double> delta)
    {
        return Offset(delta.X, delta.Y, delta.Z);
    }

    public bool Intersects(AABB other)
    {
        return Max.X > other.Min.X && Min.X < other.Max.X &&
               Max.Y > other.Min.Y && Min.Y < other.Max.Y &&
               Max.Z > other.Min.Z && Min.Z < other.Max.Z;
    }

    public double CalculateYOffset(AABB other, double deltaY)
    {
        if (other.Max.X <= Min.X || other.Min.X >= Max.X || other.Max.Z <= Min.Z || other.Min.Z >= Max.Z)
        {
            return deltaY;
        }

        if (deltaY > 0 && other.Min.Y >= Max.Y)
        {
            double dist = other.Min.Y - Max.Y;
            if (dist < deltaY) deltaY = dist;
        }
        else if (deltaY < 0 && other.Max.Y <= Min.Y)
        {
            double dist = other.Max.Y - Min.Y;
            if (dist > deltaY) deltaY = dist;
        }

        return deltaY;
    }

    public double CalculateXOffset(AABB other, double deltaX)
    {
        if (other.Max.Y <= Min.Y || other.Min.Y >= Max.Y || other.Max.Z <= Min.Z || other.Min.Z >= Max.Z)
        {
            return deltaX;
        }

        if (deltaX > 0 && other.Min.X >= Max.X)
        {
            var dist = other.Min.X - Max.X;
            if (dist < deltaX) deltaX = dist;
        }
        else if (deltaX < 0 && other.Max.X <= Min.X)
        {
            var dist = other.Max.X - Min.X;
            if (dist > deltaX) deltaX = dist;
        }

        return deltaX;
    }

    public double CalculateZOffset(AABB other, double deltaZ)
    {
        if (other.Max.X <= Min.X || other.Min.X >= Max.X || other.Max.Y <= Min.Y || other.Min.Y >= Max.Y)
        {
            return deltaZ;
        }

        if (deltaZ > 0 && other.Min.Z >= Max.Z)
        {
            var dist = other.Min.Z - Max.Z;
            if (dist < deltaZ) deltaZ = dist;
        }
        else if (deltaZ < 0 && other.Max.Z <= Min.Z)
        {
            var dist = other.Max.Z - Min.Z;
            if (dist > deltaZ) deltaZ = dist;
        }

        return deltaZ;
    }

    public override string ToString()
    {
        return $"AABB({Min.X:F3},{Min.Y:F3},{Min.Z:F3} -> {Max.X:F3},{Max.Y:F3},{Max.Z:F3})";
    }
}
