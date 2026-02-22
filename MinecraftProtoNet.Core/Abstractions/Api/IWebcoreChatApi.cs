using MinecraftProtoNet.Core.Dtos;
using Refit;

namespace MinecraftProtoNet.Core.Abstractions.Api;

/// <summary>
/// Defines the API for redirecting chat messages to the Webcore dashboard.
/// </summary>
public interface IWebcoreChatApi
{
    /// <summary>
    /// Posts a redirected chat message to the dashboard.
    /// </summary>
    /// <param name="request">The chat redirection request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Post("/api/chat/redirect")]
    Task PostRedirectedChatAsync([Body] ChatRedirectRequest request, CancellationToken ct = default);
}
