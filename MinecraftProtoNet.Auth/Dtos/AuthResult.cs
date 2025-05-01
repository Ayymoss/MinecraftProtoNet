using System.Security.Cryptography;

namespace MinecraftProtoNet.Auth.Dtos;

public class AuthResult(string username, Guid uuid, string minecraftAccessToken, RSA? playerPrivateKey, ChatSessionInfo? chatSession)
{
    public string Username { get; } = username;
    public Guid Uuid { get; } = uuid;
    public string MinecraftAccessToken { get; } = minecraftAccessToken;
    public RSA? PlayerPrivateKey { get; } = playerPrivateKey;
    public ChatSessionInfo? ChatSession { get; } = chatSession;
}
