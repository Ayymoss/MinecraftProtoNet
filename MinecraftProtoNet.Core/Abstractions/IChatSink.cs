namespace MinecraftProtoNet.Core.Abstractions;

/// <summary>
/// Defines a sink for Minecraft chat messages.
/// </summary>
public interface IChatSink
{
    /// <summary>
    /// Emits a chat message to the sink.
    /// </summary>
    /// <param name="message">The message to emit.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmitAsync(string message, CancellationToken ct = default);
}
