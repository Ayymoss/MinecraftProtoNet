namespace MinecraftProtoNet.Enums;

/// <summary>
/// Represents the loading status of a chunk.
/// Equivalent to Java's net.minecraft.world.level.chunk.status.ChunkStatus.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:115
/// Used by Baritone for chunk access validation.
/// </summary>
public enum ChunkStatus
{
    /// <summary>
    /// Chunk is fully loaded and contains complete block data.
    /// This is the status required by Baritone's BlockStateInterface.
    /// </summary>
    Full = 0
}

