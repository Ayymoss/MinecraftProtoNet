using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Handles sign editor packets. Publishes events via ISignEventBus so external
/// systems (e.g., Bazaar trading) can auto-respond to sign editor prompts.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundOpenSignEditorPacket.java
/// </summary>
[HandlesPacket(typeof(OpenSignEditorPacket))]
public class SignHandler(ILogger<SignHandler> logger, ISignEventBus signEventBus) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(SignHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        if (packet is not OpenSignEditorPacket signPacket)
            return;

        logger.LogInformation("Sign editor opened at {Position} (front={IsFront})",
            signPacket.Position, signPacket.IsFrontText);

        // Notify subscribers — they can set ResponseLines to auto-respond
        var args = await signEventBus.PublishSignEditorOpenedAsync(signPacket.Position, signPacket.IsFrontText);

        // If a subscriber claimed the event and provided response lines, auto-send
        if (args.Handled && args.ResponseLines is not null)
        {
            logger.LogInformation("Auto-responding to sign editor with: {Lines}",
                string.Join(" | ", args.ResponseLines));

            await client.SendPacketAsync(new SignUpdatePacket
            {
                Position = signPacket.Position,
                IsFrontText = signPacket.IsFrontText,
                Lines = args.ResponseLines
            });
        }
    }
}
