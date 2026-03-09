namespace MinecraftProtoNet.Core.Abstractions;

/// <summary>
/// Provides human-like timing variance, rotation jitter, and behavioral randomization
/// to avoid anti-cheat detection. All timing methods return randomized values within
/// configured ranges. Thread-safe.
/// </summary>
public interface IHumanizer
{
    /// <summary>Whether humanization is active (always true on remote servers if ForceOnRemote).</summary>
    bool IsEnabled { get; }

    /// <summary>Whether the bot is connected to a remote (non-local) server.</summary>
    bool IsRemoteServer { get; }

    /// <summary>Returns tick jitter in milliseconds to add to the game loop sleep.</summary>
    int GetTickJitterMs();

    /// <summary>Returns small rotation offsets to add to outgoing position packets (not stored on entity).</summary>
    (float yawOffset, float pitchOffset) GetRotationJitter();

    /// <summary>Returns a randomized delay for GUI click interactions.</summary>
    int GetGuiClickDelayMs();

    /// <summary>Returns a randomized delay for GUI navigation transitions (screen changes).</summary>
    int GetGuiNavigationDelayMs();

    /// <summary>Returns a randomized delay to insert before sending a chat command.</summary>
    int GetChatCommandDelayMs();

    /// <summary>Returns a randomized post-action pause (after completing a trade, claiming, etc.).</summary>
    int GetPostActionDelayMs();

    /// <summary>
    /// Picks a random nearby entity to look at for idle behavior.
    /// Returns target (yaw, pitch) or null if no suitable entity found.
    /// </summary>
    (float yaw, float pitch)? GetIdleLookTarget(
        Models.Core.Vector3<double> playerPosition,
        Models.Core.Vector2<float> currentYawPitch,
        State.WorldEntityRegistry worldEntities);

    /// <summary>
    /// Returns true if the bot should perform an idle action this tick.
    /// Call once per tick; internally tracks time since last idle action.
    /// </summary>
    bool ShouldPerformIdleAction();
}
