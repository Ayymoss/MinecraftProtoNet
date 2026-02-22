namespace MinecraftProtoNet.Core.State.Base;

/// <summary>
/// Stores bot-specific configuration settings.
/// </summary>
public class BotSettings
{
    /// <summary>
    /// Whether to redirect chat messages to an external sink (e.g., Webcore) instead of sending them directly to the server.
    /// This is used to reduce bot detection by allowing manual review.
    /// </summary>
    public bool RedirectChat { get; set; }
}
