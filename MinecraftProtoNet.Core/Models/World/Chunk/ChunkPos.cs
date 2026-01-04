namespace MinecraftProtoNet.Models.World.Chunk;

/// <summary>
/// Represents chunk coordinates (chunk X and Z).
/// Equivalent to Java's net.minecraft.world.level.ChunkPos.
/// Used for chunk coordinate representation and arithmetic.
/// Reference: Used extensively in Baritone for chunk iteration and caching.
/// </summary>
public struct ChunkPos : IEquatable<ChunkPos>
{
    /// <summary>
    /// Chunk X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Chunk Z coordinate.
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Creates a new ChunkPos with the specified coordinates.
    /// </summary>
    public ChunkPos(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>
    /// Creates a ChunkPos from world block coordinates.
    /// </summary>
    public static ChunkPos FromBlockPos(int blockX, int blockZ)
    {
        return new ChunkPos(blockX >> 4, blockZ >> 4);
    }

    /// <summary>
    /// Gets the minimum block X coordinate in this chunk.
    /// </summary>
    public int MinBlockX => X << 4;

    /// <summary>
    /// Gets the maximum block X coordinate in this chunk.
    /// </summary>
    public int MaxBlockX => (X << 4) + 15;

    /// <summary>
    /// Gets the minimum block Z coordinate in this chunk.
    /// </summary>
    public int MinBlockZ => Z << 4;

    /// <summary>
    /// Gets the maximum block Z coordinate in this chunk.
    /// </summary>
    public int MaxBlockZ => (Z << 4) + 15;

    /// <summary>
    /// Calculates the distance squared between two chunk positions.
    /// </summary>
    public int DistSqr(ChunkPos other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Gets a ChunkPos offset by the specified amounts.
    /// </summary>
    public ChunkPos Offset(int dx, int dz)
    {
        return new ChunkPos(X + dx, Z + dz);
    }

    public bool Equals(ChunkPos other)
    {
        return X == other.X && Z == other.Z;
    }

    public override bool Equals(object? obj)
    {
        return obj is ChunkPos other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Z);
    }

    public static bool operator ==(ChunkPos left, ChunkPos right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ChunkPos left, ChunkPos right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"ChunkPos({X}, {Z})";
    }
}

