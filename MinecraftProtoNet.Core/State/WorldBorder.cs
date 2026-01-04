namespace MinecraftProtoNet.State;

/// <summary>
/// Represents the world border with bounds checking.
/// Equivalent to Java's net.minecraft.world.level.border.WorldBorder.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java
/// Used by Baritone for pathfinding bounds validation.
/// </summary>
public class WorldBorder
{
    /// <summary>
    /// Center X coordinate of the world border.
    /// </summary>
    public double CenterX { get; set; }

    /// <summary>
    /// Center Z coordinate of the world border.
    /// </summary>
    public double CenterZ { get; set; }

    /// <summary>
    /// Current diameter of the world border.
    /// </summary>
    public double Diameter { get; set; } = double.MaxValue; // Default: infinite (no border)

    /// <summary>
    /// Previous diameter (for interpolation during border changes).
    /// </summary>
    public double OldDiameter { get; set; } = double.MaxValue;

    /// <summary>
    /// Speed at which the border changes (blocks per second).
    /// </summary>
    public long Speed { get; set; }

    /// <summary>
    /// Portal teleport boundary distance.
    /// </summary>
    public int PortalTeleportBoundary { get; set; }

    /// <summary>
    /// Warning blocks - distance from border at which warning appears.
    /// </summary>
    public int WarningBlocks { get; set; }

    /// <summary>
    /// Warning time - time in seconds for warning before border reaches player.
    /// </summary>
    public int WarningTime { get; set; }

    /// <summary>
    /// Minimum X coordinate of the world border.
    /// </summary>
    public double MinX => CenterX - Diameter / 2.0;

    /// <summary>
    /// Maximum X coordinate of the world border.
    /// </summary>
    public double MaxX => CenterX + Diameter / 2.0;

    /// <summary>
    /// Minimum Z coordinate of the world border.
    /// </summary>
    public double MinZ => CenterZ - Diameter / 2.0;

    /// <summary>
    /// Maximum Z coordinate of the world border.
    /// </summary>
    public double MaxZ => CenterZ + Diameter / 2.0;

    /// <summary>
    /// Checks if a position is within the world border bounds.
    /// </summary>
    public bool Contains(double x, double z)
    {
        // If diameter is infinite (double.MaxValue), always return true
        if (Diameter >= double.MaxValue / 2.0) return true;

        return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }

    /// <summary>
    /// Gets the distance from the border for a given position.
    /// Returns negative if outside the border, positive if inside.
    /// </summary>
    public double GetDistanceFromBorder(double x, double z)
    {
        if (Diameter >= double.MaxValue / 2.0) return double.MaxValue; // Infinite border

        var distX = Math.Min(Math.Abs(x - MinX), Math.Abs(x - MaxX));
        var distZ = Math.Min(Math.Abs(z - MinZ), Math.Abs(z - MaxZ));
        return Math.Min(distX, distZ);
    }
}

