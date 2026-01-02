using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles player info and tab list packets.
/// </summary>
[HandlesPacket(typeof(PlayerInfoUpdatePacket))]
[HandlesPacket(typeof(PlayerInfoRemovePacket))]
public class PlayerInfoHandler() : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(PlayerInfoHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case PlayerInfoUpdatePacket playerInfoUpdatePacket:
                foreach (var info in playerInfoUpdatePacket.PlayerInfos)
                {
                    foreach (var action in info.Actions)
                    {
                        switch (action)
                        {
                            // 'AddPlayer' should always come in first
                            case PlayerInfoUpdatePacket.AddPlayer addPlayer:
                            {
                                var player = await client.State.Level.AddPlayerAsync(info.Uuid, addPlayer.Username);
                                player.Properties = addPlayer.Properties.ToList();
                                break;
                            }
                            case PlayerInfoUpdatePacket.UpdateGameMode updateGameMode:
                            {
                                var player = client.State.Level.GetPlayerByUuid(info.Uuid);
                                if (player is null) break;
                                player.GameMode = updateGameMode.GameMode;
                                break;
                            }
                            case PlayerInfoUpdatePacket.UpdateLatency updateLatency:
                            {
                                var player = client.State.Level.GetPlayerByUuid(info.Uuid);
                                if (player is null) break;
                                player.Latency = updateLatency.Latency;
                                break;
                            }
                        }
                    }
                }

                break;

            case PlayerInfoRemovePacket playerInfoRemovePacket:
                foreach (var uuid in playerInfoRemovePacket.Uuids)
                {
                    await client.State.Level.RemovePlayerAsync(uuid);
                }

                break;
        }
    }
}
