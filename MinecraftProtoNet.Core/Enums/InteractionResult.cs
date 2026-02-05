namespace MinecraftProtoNet.Core.Enums;

/// <summary>
/// Represents the result of an interaction (block placement, entity interaction, etc.).
/// Equivalent to Java's net.minecraft.world.InteractionResult.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerController.java:50-52
/// Used by Baritone for interaction return values.
/// </summary>
public enum InteractionResult
{
    /// <summary>
    /// Interaction succeeded and should continue processing.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Interaction succeeded and should consume the item/action (stop processing).
    /// </summary>
    Consume = 1,

    /// <summary>
    /// Interaction should be passed to the next handler (no action taken).
    /// </summary>
    Pass = 2,

    /// <summary>
    /// Interaction failed.
    /// </summary>
    Fail = 3
}

