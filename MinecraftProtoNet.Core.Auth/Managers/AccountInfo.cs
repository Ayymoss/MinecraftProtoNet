namespace MinecraftProtoNet.Core.Auth.Managers;

/// <summary>
/// Lightweight view of a cached Microsoft account, used by the web UI.
/// </summary>
public record AccountInfo(string HomeAccountId, string Username, bool IsActive);
