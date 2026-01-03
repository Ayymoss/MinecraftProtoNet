namespace MinecraftProtoNet.Baritone.Pathfinding.Calc;

/// <summary>
/// World border checks for pathfinding.
/// Prevents movements from venturing into the world border and prevents block placement at the border edge.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java
/// </summary>
public class BetterWorldBorder
{
    private readonly double _minX;
    private readonly double _maxX;
    private readonly double _minZ;
    private readonly double _maxZ;

    /// <summary>
    /// Creates a world border checker from world border bounds.
    /// </summary>
    /// <param name="minX">Minimum X coordinate of the world border</param>
    /// <param name="maxX">Maximum X coordinate of the world border</param>
    /// <param name="minZ">Minimum Z coordinate of the world border</param>
    /// <param name="maxZ">Maximum Z coordinate of the world border</param>
    public BetterWorldBorder(double minX, double maxX, double minZ, double maxZ)
    {
        _minX = minX;
        _maxX = maxX;
        _minZ = minZ;
        _maxZ = maxZ;
    }

    /// <summary>
    /// Checks if a block position is entirely within the world border.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java:40-42
    /// </summary>
    /// <param name="x">Block X coordinate</param>
    /// <param name="z">Block Z coordinate</param>
    /// <returns>True if the block is entirely within the border</returns>
    public bool EntirelyContains(int x, int z)
    {
        // Check if block [x, x+1) x [z, z+1) is entirely within [minX, maxX) x [minZ, maxZ)
        return x + 1 > _minX && x < _maxX && z + 1 > _minZ && z < _maxZ;
    }

    /// <summary>
    /// Checks if a block can be placed at this position (1 block margin from edge).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java:44-49
    /// </summary>
    /// <param name="x">Block X coordinate</param>
    /// <param name="z">Block Z coordinate</param>
    /// <returns>True if a block can be placed (not at the very edge)</returns>
    public bool CanPlaceAt(int x, int z)
    {
        // Move it in 1 block on all sides because we can't place a block at the very edge
        // against a block outside the border - it won't let us right click it
        return x > _minX && x + 1 < _maxX && z > _minZ && z + 1 < _maxZ;
    }
}

