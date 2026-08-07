using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Surfaces text the server paints on the screen rather than into chat: titles, subtitles and the action bar.
///
/// A player sees these; a bot that only reads chat does not. Staff addressing someone mid-investigation
/// routinely use a title, so anything watching for intervention needs this stream as much as the chat one.
/// </summary>
[HandlesPacket(typeof(SetTitleTextPacket))]
[HandlesPacket(typeof(SetSubtitleTextPacket))]
[HandlesPacket(typeof(SetActionBarTextPacket))]
[HandlesPacket(typeof(OpenBookPacket))]
[HandlesPacket(typeof(ShowDialogPacket))]
public class ScreenTextHandler(ILogger<ScreenTextHandler> logger, IChatEventBus chatEventBus) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ScreenTextHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        var (kind, text) = packet switch
        {
            SetTitleTextPacket title => (ScreenTextKind.Title, title.Text),
            SetSubtitleTextPacket subtitle => (ScreenTextKind.Subtitle, subtitle.Text),
            SetActionBarTextPacket actionBar => (ScreenTextKind.ActionBar, actionBar.Text),
            OpenBookPacket book => (ScreenTextKind.Book, $"server forced a book open in the {book.Hand}"),
            ShowDialogPacket dialog => (ScreenTextKind.Dialog,
                $"server pushed a dialog ({dialog.RawPayload.Length} bytes): {dialog.PayloadHex}"),
            _ => (ScreenTextKind.Title, string.Empty)
        };

        if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

        logger.LogDebug("Screen text ({Kind}): {Text}", kind, text);
        chatEventBus.PublishScreenText(kind, text);
        return Task.CompletedTask;
    }
}
