using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Abstractions.Api;
using MinecraftProtoNet.Core.Dtos;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// A chat sink that redirects messages to the Webcore dashboard via an API.
/// </summary>
/// <param name="api">The Webcore chat API client.</param>
/// <param name="logger">The logger instance.</param>
public sealed class WebcoreChatSink(IWebcoreChatApi api, ILogger<WebcoreChatSink> logger) : IChatSink
{
    /// <inheritdoc />
    public async Task EmitAsync(string message, CancellationToken ct = default)
    {
        try
        {
            var timestamp = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds();
            var request = new ChatRedirectRequest(message, timestamp);
            await api.PostRedirectedChatAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to redirect chat message to dashboard. Ensure the web server is running and the URL is correct.");
            throw; // Re-throw to ensure the command or caller knows it failed
        }
    }
}
