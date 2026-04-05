using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Services;
using KeepAlivePacket = MinecraftProtoNet.Core.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Handles keep-alive, ping, resource packs, and connection maintenance packets.
/// </summary>
[HandlesPacket(typeof(KeepAlivePacket))]
[HandlesPacket(typeof(PingPacket))]
[HandlesPacket(typeof(PongResponsePacket))]
[HandlesPacket(typeof(ChunkBatchFinishedPacket))]
[HandlesPacket(typeof(ResourcePackPushPacket))]
[HandlesPacket(typeof(ResourcePackPopPacket))]
[HandlesPacket(typeof(Packets.Play.Clientbound.CustomPayloadPacket))]
public class ConnectionHandler(ILogger<ConnectionHandler> logger, IHumanizer humanizer) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ConnectionHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case KeepAlivePacket keepAlivePacket:
                await client.SendPacketAsync(new Packets.Play.Serverbound.KeepAlivePacket
                {
                    Payload = keepAlivePacket.Payload
                });
                break;

            case PingPacket pingPacket:
                // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java
                // Vanilla only sends Pong in response to Ping. SetCarriedItem and ClientTickEnd
                // come from the game loop, not from the Ping handler. Sending extra packets here
                // can hit a Velocity race condition where getConnectedServer() is still null.
                await client.SendPacketAsync(new PongPacket { Payload = pingPacket.Id });
                break;

            case PongResponsePacket pongResponsePacket:
                logger.LogDebug("Pong response: {Payload}", pongResponsePacket.Payload);
                break;

            case ChunkBatchFinishedPacket chunkBatchFinishedPacket:
                await client.SendPacketAsync(new ChunkBatchReceivedPacket
                {
                    DesiredChunksPerTick = chunkBatchFinishedPacket.BatchSize
                });
                break;

            case ResourcePackPushPacket resourcePackPushPacket:
            {
                // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientCommonPacketListenerImpl.java:170-186
                // Vanilla flow: receive push → validate URL → show prompt/auto-accept → download → send SUCCESSFULLY_LOADED
                // Bot flow: accept immediately, simulate download time, then report loaded.
                logger.LogInformation("Resource pack push: PackId={PackId}, Url={Url}, Required={Required}",
                    resourcePackPushPacket.PackId, resourcePackPushPacket.Url, resourcePackPushPacket.Required);

                // Step 1: Send ACCEPTED (vanilla does this when user clicks accept or auto-accept is on)
                await client.SendPacketAsync(new ResourcePackPacket
                {
                    PackId = resourcePackPushPacket.PackId,
                    Action = ResourcePackPacket.ResourcePackAction.Accepted
                });

                // Step 2: Send DOWNLOADED (vanilla sends this when download completes)
                // Add humanized delay to simulate download time (real clients take 1-5+ seconds)
                var downloadDelay = humanizer.IsEnabled
                    ? Random.Shared.Next(800, 2500)
                    : Random.Shared.Next(500, 1500);
                await Task.Delay(downloadDelay);

                await client.SendPacketAsync(new ResourcePackPacket
                {
                    PackId = resourcePackPushPacket.PackId,
                    Action = ResourcePackPacket.ResourcePackAction.Downloaded
                });

                // Step 3: Send SUCCESSFULLY_LOADED (vanilla sends this after applying the pack)
                var applyDelay = humanizer.IsEnabled
                    ? Random.Shared.Next(200, 600)
                    : Random.Shared.Next(100, 300);
                await Task.Delay(applyDelay);

                await client.SendPacketAsync(new ResourcePackPacket
                {
                    PackId = resourcePackPushPacket.PackId,
                    Action = ResourcePackPacket.ResourcePackAction.SuccessfullyLoaded
                });

                logger.LogInformation("Resource pack accepted and loaded: PackId={PackId}", resourcePackPushPacket.PackId);
                break;
            }

            case ResourcePackPopPacket resourcePackPopPacket:
                // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientCommonPacketListenerImpl.java:188-191
                // Just acknowledge the pop — we don't actually store resource packs.
                logger.LogDebug("Resource pack pop: PackId={PackId}", resourcePackPopPacket.PackId?.ToString() ?? "all");
                break;

            case Packets.Play.Clientbound.CustomPayloadPacket customPayloadPacket:
                // Detect ViaVersion via plugin channel registration.
                // ViaVersion registers "vv:server_details" (or legacy "MC|BSign") when proxying.
                // This flag prevents sending ChatSessionUpdate to backends that can't handle it.
                if (customPayloadPacket.Channel is "vv:server_details" or "viaversion:config")
                {
                    if (!client.State.ServerSettings.HasViaVersion)
                    {
                        client.State.ServerSettings.HasViaVersion = true;
                        logger.LogInformation("ViaVersion detected via plugin channel: {Channel}", customPayloadPacket.Channel);
                    }
                }
                break;
        }
    }
}
