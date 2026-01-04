namespace MinecraftProtoNet.Enums;

/// <summary>
/// Represents the type of a raycast hit result.
/// Equivalent to Java's net.minecraft.world.phys.HitResult.Type.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java:120-122
/// Used by Baritone for raycast result type checking.
/// </summary>
public enum HitResultType
{
    /// <summary>
    /// Raycast missed (no hit).
    /// </summary>
    Miss = 0,

    /// <summary>
    /// Raycast hit a block.
    /// </summary>
    Block = 1,

    /// <summary>
    /// Raycast hit an entity.
    /// </summary>
    Entity = 2
}

