namespace MinecraftProtoNet.Auth.Dtos;

public class ChatSessionInfo
{
    public ChatContext ChatContext { get; set; } = new();

    public byte[] PublicKeyDer { get; init; }
    public long ExpiresAtEpochMs { get; init; }
    public byte[] MojangSignature { get; init; }
}
