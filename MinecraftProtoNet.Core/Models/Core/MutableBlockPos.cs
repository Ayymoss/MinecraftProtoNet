using MinecraftProtoNet.Core.Enums;

namespace MinecraftProtoNet.Core.Models.Core;

/// <summary>
/// Mutable block position for efficient position updates without allocation.
/// Equivalent to Java's net.minecraft.core.BlockPos.MutableBlockPos.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:47
/// Used by Baritone for mutable block position operations.
/// </summary>
public class MutableBlockPos
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public MutableBlockPos()
    {
    }

    public MutableBlockPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public MutableBlockPos(Vector3<int> pos)
    {
        X = pos.X;
        Y = pos.Y;
        Z = pos.Z;
    }

    /// <summary>
    /// Sets the position to the specified coordinates.
    /// Equivalent to Java's MutableBlockPos.set(int x, int y, int z).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/BlockPos.java:MutableBlockPos.set()
    /// </summary>
    public MutableBlockPos Set(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
        return this;
    }

    /// <summary>
    /// Sets the position from another Vector3&lt;int&gt;.
    /// </summary>
    public MutableBlockPos Set(Vector3<int> pos)
    {
        X = pos.X;
        Y = pos.Y;
        Z = pos.Z;
        return this;
    }

    /// <summary>
    /// Moves the position in the specified direction.
    /// Equivalent to Java's MutableBlockPos.move(Direction).
    /// </summary>
    public MutableBlockPos Move(BlockFace face, int steps = 1)
    {
        return face switch
        {
            BlockFace.Bottom => Set(X, Y - steps, Z),
            BlockFace.Top => Set(X, Y + steps, Z),
            BlockFace.North => Set(X, Y, Z - steps),
            BlockFace.South => Set(X, Y, Z + steps),
            BlockFace.West => Set(X - steps, Y, Z),
            BlockFace.East => Set(X + steps, Y, Z),
            _ => this
        };
    }

    /// <summary>
    /// Moves the position up (Y+).
    /// </summary>
    public MutableBlockPos MoveUp(int steps = 1)
    {
        return Move(BlockFace.Top, steps);
    }

    /// <summary>
    /// Moves the position down (Y-).
    /// </summary>
    public MutableBlockPos MoveDown(int steps = 1)
    {
        return Move(BlockFace.Bottom, steps);
    }

    /// <summary>
    /// Moves the position north (Z-).
    /// </summary>
    public MutableBlockPos MoveNorth(int steps = 1)
    {
        return Move(BlockFace.North, steps);
    }

    /// <summary>
    /// Moves the position south (Z+).
    /// </summary>
    public MutableBlockPos MoveSouth(int steps = 1)
    {
        return Move(BlockFace.South, steps);
    }

    /// <summary>
    /// Moves the position east (X+).
    /// </summary>
    public MutableBlockPos MoveEast(int steps = 1)
    {
        return Move(BlockFace.East, steps);
    }

    /// <summary>
    /// Moves the position west (X-).
    /// </summary>
    public MutableBlockPos MoveWest(int steps = 1)
    {
        return Move(BlockFace.West, steps);
    }

    /// <summary>
    /// Converts this mutable position to an immutable Vector3&lt;int&gt;.
    /// </summary>
    public Vector3<int> ToImmutable()
    {
        return new Vector3<int>(X, Y, Z);
    }

    /// <summary>
    /// Implicit conversion to Vector3&lt;int&gt;.
    /// </summary>
    public static implicit operator Vector3<int>(MutableBlockPos pos)
    {
        return new Vector3<int>(pos.X, pos.Y, pos.Z);
    }

    /// <summary>
    /// Explicit conversion from Vector3&lt;int&gt;.
    /// </summary>
    public static explicit operator MutableBlockPos(Vector3<int> pos)
    {
        return new MutableBlockPos(pos);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is MutableBlockPos other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
        if (obj is Vector3<int> vec)
        {
            return X == vec.X && Y == vec.Y && Z == vec.Z;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
}

