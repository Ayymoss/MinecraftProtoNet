using MinecraftProtoNet.Core.Actions;
using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Core;

public interface IMinecraftClient : IPacketSender
{
    ProtocolState ProtocolState { get; set; }

    /// <summary>
    /// Set when the server sends a Transfer packet: the host/port it wants us to reconnect to.
    /// Null when there is no outstanding transfer. The session owner is responsible for acting on it — the
    /// reconnect re-runs authentication and the join sequence, which a packet handler must not do.
    /// </summary>
    (string Host, int Port)? PendingTransfer { get; set; }
    ClientState State { get; }
    int ProtocolVersion { get; set; }
    AuthResult? AuthResult { get; set; }

    /// <summary>
    /// Whether the client is currently connected to a server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the interaction manager.
    /// </summary>
    IInteractionManager InteractionManager { get; }

    /// <summary>
    /// Raised when the client disconnects from the server.
    /// </summary>
    event EventHandler<DisconnectReason>? OnDisconnected;

    Task<bool> AuthenticateAsync();
    void EnableEncryption(byte[] sharedSecret);
    void EnableCompression(int threshold);

    /// <summary>
    /// Per-second outbound packet counts and composition for the recent past, for explaining a connection the
    /// server ended on its own terms. The window is rolling, so read it as soon as something goes wrong.
    /// </summary>
    string DumpRecentOutbound(int seconds = 45);

    /// <summary>The last packets seen in each direction, interleaved by time. Finer than the per-second view.</summary>
    string DumpRecentPackets();
    Task ConnectAsync(string host, int port, bool isSnapshot = false);
    Task DisconnectAsync();

    /// <summary>
    /// Sends a chat message, optionally redirecting it based on bot settings.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendChatMessageAsync(string message, CancellationToken ct = default);

    Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage);

    /// <summary>
    /// Performs a physics tick for the local player.
    /// </summary>
    /// <param name="prePhysicsCallback">Optional callback for pathfinding or AI logic</param>
    Task PhysicsTickAsync(Action<State.Entity>? prePhysicsCallback = null);

    /// <summary>
    /// Queues work to run at the point in the tick where vanilla handles input, i.e. BEFORE the movement
    /// packet for that tick is sent.
    ///
    /// Vanilla's order is pick() -> handleKeybinds() -> player.tick() -> sendPosition(), so an interact
    /// always precedes that tick's movement packet. Ours were sent straight from async tasks and landed
    /// wherever the scheduler put them — usually AFTER the movement packet, which GrimAC flags as
    /// "Post: interact entity" on every single NPC interaction. Menu opens being the confirmed ejection
    /// trigger, that ordering is the most likely reason opening a menu is what gets us thrown out.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/Minecraft.java (tick order)
    /// </summary>
    void EnqueuePreMovementAction(Func<Task> action);

    /// <summary>Runs everything queued by <see cref="EnqueuePreMovementAction"/>. Called by the game loop.</summary>
    Task DrainPreMovementActionsAsync();

    Task SendChatSessionUpdate();

    /// <summary>
    /// Checks if the current thread is the main/game thread.
    /// Equivalent to Java's Minecraft.isSameThread().
    /// Used by Baritone to validate thread safety for certain operations.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:72-74
    /// </summary>
    bool IsSameThread();
}
