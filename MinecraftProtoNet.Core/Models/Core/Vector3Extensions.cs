using MinecraftProtoNet.Core.Enums;

namespace MinecraftProtoNet.Core.Models.Core;

/// <summary>
/// Extension methods for Vector3&lt;int&gt; to provide BlockPos-like immutable operations.
/// Equivalent to Java's net.minecraft.core.Vec3i operations (above, below, relative, etc.).
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:108-162
/// Used by Baritone for block position operations.
/// </summary>
public static class Vector3IntExtensions
{
    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block above this position.
    /// Equivalent to Java's Vec3i.above().
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:108-110
    /// </summary>
    public static Vector3<int> Above(this Vector3<int> pos)
    {
        return Above(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks above this position.
    /// Equivalent to Java's Vec3i.above(int steps).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:112-114
    /// </summary>
    public static Vector3<int> Above(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.Top, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block below this position.
    /// Equivalent to Java's Vec3i.below().
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:116-118
    /// </summary>
    public static Vector3<int> Below(this Vector3<int> pos)
    {
        return Below(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks below this position.
    /// Equivalent to Java's Vec3i.below(int steps).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:120-122
    /// </summary>
    public static Vector3<int> Below(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.Bottom, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block north of this position.
    /// Equivalent to Java's Vec3i.north().
    /// </summary>
    public static Vector3<int> North(this Vector3<int> pos)
    {
        return North(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks north of this position.
    /// Equivalent to Java's Vec3i.north(int steps).
    /// </summary>
    public static Vector3<int> North(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.North, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block south of this position.
    /// Equivalent to Java's Vec3i.south().
    /// </summary>
    public static Vector3<int> South(this Vector3<int> pos)
    {
        return South(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks south of this position.
    /// Equivalent to Java's Vec3i.south(int steps).
    /// </summary>
    public static Vector3<int> South(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.South, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block east of this position.
    /// Equivalent to Java's Vec3i.east().
    /// </summary>
    public static Vector3<int> East(this Vector3<int> pos)
    {
        return East(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks east of this position.
    /// Equivalent to Java's Vec3i.east(int steps).
    /// </summary>
    public static Vector3<int> East(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.East, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; one block west of this position.
    /// Equivalent to Java's Vec3i.west().
    /// </summary>
    public static Vector3<int> West(this Vector3<int> pos)
    {
        return West(pos, 1);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; the specified number of blocks west of this position.
    /// Equivalent to Java's Vec3i.west(int steps).
    /// </summary>
    public static Vector3<int> West(this Vector3<int> pos, int steps)
    {
        return Relative(pos, BlockFace.West, steps);
    }

    /// <summary>
    /// Returns a new Vector3&lt;int&gt; relative to this position in the specified direction.
    /// Equivalent to Java's Vec3i.relative(Direction, int steps).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/Vec3i.java:160-162
    /// </summary>
    public static Vector3<int> Relative(this Vector3<int> pos, BlockFace face, int steps = 1)
    {
        if (steps == 0) return pos;

        return face switch
        {
            BlockFace.Bottom => new Vector3<int>(pos.X, pos.Y - steps, pos.Z),
            BlockFace.Top => new Vector3<int>(pos.X, pos.Y + steps, pos.Z),
            BlockFace.North => new Vector3<int>(pos.X, pos.Y, pos.Z - steps),
            BlockFace.South => new Vector3<int>(pos.X, pos.Y, pos.Z + steps),
            BlockFace.West => new Vector3<int>(pos.X - steps, pos.Y, pos.Z),
            BlockFace.East => new Vector3<int>(pos.X + steps, pos.Y, pos.Z),
            _ => pos
        };
    }

    /// <summary>
    /// Calculates the squared distance between two block positions.
    /// Equivalent to Java's BlockPos.distSqr(BlockPos).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/BuilderProcess.java:628
    /// </summary>
    public static double DistSqr(this Vector3<int> pos, Vector3<int> other)
    {
        var dx = pos.X - other.X;
        var dy = pos.Y - other.Y;
        var dz = pos.Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Calculates the Manhattan distance between two block positions.
    /// Equivalent to Java's Vec3i.distManhattan(Vec3i).
    /// </summary>
    public static int DistManhattan(this Vector3<int> pos, Vector3<int> other)
    {
        return Math.Abs(pos.X - other.X) + Math.Abs(pos.Y - other.Y) + Math.Abs(pos.Z - other.Z);
    }
}

