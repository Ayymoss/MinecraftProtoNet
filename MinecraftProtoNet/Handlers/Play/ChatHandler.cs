using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags.Primitive;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles chat-related packets.
/// </summary>
[HandlesPacket(typeof(SystemChatPacket))]
[HandlesPacket(typeof(PlayerChatPacket))]
[HandlesPacket(typeof(DisconnectPacket))]
[HandlesPacket(typeof(PlayerCombatKillPacket))]
public class ChatHandler(ILogger<ChatHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ChatHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case SystemChatPacket systemChatPacket:
            {
                var translateLookup = systemChatPacket.Tags.FindTag<NbtString>("translate")?.Value;
                var texts = systemChatPacket.Tags.FindTags<NbtString>("text").Reverse().Select(x => x.Value);
                logger.LogInformation("System message: ({TranslateKey}) {Messages}",
                    translateLookup ?? "<NULL>", string.Join(" ", texts));
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

                if (signatureBytes is not null)
                {
                    ChatSigning.ChatMessageReceived(client.AuthResult, signatureBytes);
                }

                _ = Task.Run(async () => await client.HandleChatMessageAsync(playerChatPacket.Header.Uuid, playerChatPacket.Body.Message));
                break;
            }
            
            case DisconnectPacket disconnectPacket:
            {
                var translateLookup = disconnectPacket.DisconnectReason.FindTag<NbtString>("translate")?.Value;
                var messages = disconnectPacket.DisconnectReason.FindTags<NbtString>(null).Reverse().Select(x => x.Value);
                logger.LogWarning("Disconnected from server: ({TranslateKey}) {Messages}",
                    translateLookup, string.Join(" ", messages));
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
