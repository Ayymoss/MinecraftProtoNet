namespace MinecraftProtoNet.Core.Dtos;

/// <summary>
/// Represents a request to redirect a Minecraft chat message.
/// </summary>
/// <param name="Message">The chat message content.</param>
/// <param name="Timestamp">The timestamp of the message.</param>
public sealed record ChatRedirectRequest(string Message, long Timestamp);
