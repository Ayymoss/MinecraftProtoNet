namespace MinecraftProtoNet.State;

/// <summary>
/// Represents dimension type properties including vertical bounds.
/// Equivalent to Java's net.minecraft.world.level.dimension.DimensionType.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:98-100
/// Used by Baritone for bounds checking in BlockStateInterface.
/// </summary>
public class DimensionType
{
    /// <summary>
    /// Minimum Y coordinate for this dimension (typically -64 for 1.18+).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/level/dimension/DimensionType.java
    /// </summary>
    public int MinY { get; set; }

    /// <summary>
    /// Height of the dimension (typically 384 for 1.18+, i.e., -64 to 320).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/level/dimension/DimensionType.java
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Creates a DimensionType with default values for 1.18+ (minY: -64, height: 384).
    /// </summary>
    public DimensionType() : this(-64, 384)
    {
    }

    /// <summary>
    /// Creates a DimensionType with the specified bounds.
    /// </summary>
    public DimensionType(int minY, int height)
    {
        MinY = minY;
        Height = height;
    }

    /// <summary>
    /// Returns the maximum Y coordinate (minY + height - 1).
    /// </summary>
    public int MaxY => MinY + Height - 1;
}

