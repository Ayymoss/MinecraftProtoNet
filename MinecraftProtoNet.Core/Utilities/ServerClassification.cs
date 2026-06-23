namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// Determines whether a server host is considered "local" (development/trusted) or "remote" (public).
/// Used to decide whether to engage humanizer timing, default chat redirection, etc.
/// The local-prefix list comes from <c>HumanizerConfig.LocalNetworks</c> so both humanizer
/// and chat-redirect logic share a single source of truth.
/// </summary>
public static class ServerClassification
{
    /// <summary>
    /// Returns true if the host is non-local (i.e. does not start with any configured local prefix).
    /// Returns false for null/empty hosts so callers can treat "not yet connected" as non-remote.
    /// </summary>
    public static bool IsRemote(string? host, IEnumerable<string> localPrefixes)
    {
        if (string.IsNullOrEmpty(host)) return false;

        foreach (var prefix in localPrefixes)
        {
            if (string.IsNullOrEmpty(prefix)) continue;
            if (host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
