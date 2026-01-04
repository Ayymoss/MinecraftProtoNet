namespace MinecraftProtoNet.Core.Auth.Dtos;

public class ChatSessionInfo
{
    public ChatContext ChatContext { get; set; } = new();

    public required byte[] PublicKeyDer { get; init; }
    public long ExpiresAtEpochMs { get; init; }
    public required byte[] MojangSignature { get; init; }
}
