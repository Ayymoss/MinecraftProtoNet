using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.NBT;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Handles chat-related packets.
/// </summary>
[HandlesPacket(typeof(SystemChatPacket))]
[HandlesPacket(typeof(PlayerChatPacket))]
[HandlesPacket(typeof(DisguisedChatPacket))]
[HandlesPacket(typeof(DisconnectPacket))]
[HandlesPacket(typeof(PlayerCombatKillPacket))]
public class ChatHandler(ILogger<ChatHandler> logger, IChatEventBus chatEventBus) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ChatHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case DisguisedChatPacket disguisedChatPacket:
            {
                var visibleText = disguisedChatPacket.Message.GetVisibleText();
                var textParts = new List<string> { visibleText };
                logger.LogInformation("[DisguisedChat] {ChatType}: {Message}",
                    disguisedChatPacket.ChatType.ChatTypeId, visibleText);

                chatEventBus.PublishSystemChat(
                    disguisedChatPacket.Message,
                    false,
                    null,
                    textParts);
                break;
            }

            case SystemChatPacket systemChatPacket:
            {
                var translateLookup = systemChatPacket.Tags.FindTag<NbtString>("translate")?.Value;
                var visibleText = systemChatPacket.Tags.GetVisibleText();
                var textParts = new List<string> { visibleText };
                logger.LogInformation("System message: ({TranslateKey}) {Message}",
                    translateLookup ?? "<NULL>", visibleText);

                chatEventBus.PublishSystemChat(
                    systemChatPacket.Tags,
                    systemChatPacket.Overlay,
                    translateLookup,
                    textParts);
                break;
            }
                
            case PlayerChatPacket playerChatPacket:
            {
                var signatureBytes = playerChatPacket.Header.MessageSignature;
                var signatureHex = signatureBytes is not null
                    ? BitConverter.ToString(signatureBytes).Replace("-", "")
                    : "None";
                
                logger.LogInformation(
                    "[Chat] {Sender}: {Body} (Signature: {Signature})",
                    playerChatPacket.Header.Uuid,
                    playerChatPacket.Body.Message,
                    signatureHex);

                if (signatureBytes is not null && client.AuthResult is not null)
                {
                    ChatSigning.ChatMessageReceived(client.AuthResult, signatureBytes);
                }

                _ = Task.Run(async () => await client.HandleChatMessageAsync(playerChatPacket.Header.Uuid, playerChatPacket.Body.Message));
                break;
            }
            
            case DisconnectPacket disconnectPacket:
            {
                var translateLookup = disconnectPacket.DisconnectReason.FindTag<NbtString>("translate")?.Value;
                var visibleText = disconnectPacket.DisconnectReason.GetVisibleText();
                logger.LogWarning("Disconnected from server: ({TranslateKey}) {Message}",
                    translateLookup, visibleText);
                break;
            }

            case PlayerCombatKillPacket playerCombatKillPacket:
            {
                logger.LogInformation("Player {PlayerId} died: {DeathMessage}",
                    playerCombatKillPacket.PlayerId, playerCombatKillPacket.DeathMessage);
                break;
            }
        }

        return Task.CompletedTask;
    }
}
