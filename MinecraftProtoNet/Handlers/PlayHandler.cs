using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags.Primitive;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler : IPacketHandler
{
    private bool _playerLoaded;

    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
    [
        (ProtocolState.Play, 0x01),
        (ProtocolState.Play, 0x0B),
        (ProtocolState.Play, 0x2C),
        (ProtocolState.Play, 0x3A),
        (ProtocolState.Play, 0x63),
        (ProtocolState.Play, 0x7E),
        (ProtocolState.Play, 0x1F),
        (ProtocolState.Play, 0x42),
        (ProtocolState.Play, 0x23),
        (ProtocolState.Play, 0x28),
        (ProtocolState.Play, 0x27),
        (ProtocolState.Play, 0x62),
        (ProtocolState.Play, 0x3E),
        (ProtocolState.Play, 0x30),
        (ProtocolState.Play, 0x2F),
        (ProtocolState.Play, 0x1D),
        (ProtocolState.Play, 0x3B),
        (ProtocolState.Play, 0x47),
        (ProtocolState.Play, 0x37),
        (ProtocolState.Play, 0x38),
        (ProtocolState.Play, 0x15),
        (ProtocolState.Play, 0x5D),
        (ProtocolState.Play, 0x40),
        (ProtocolState.Play, 0x13),
        (ProtocolState.Play, 0x73),
        (ProtocolState.Play, 0x6B),
        (ProtocolState.Play, 0x05),
    ];

    public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case SetTimePacket setTimePacket:
            {
                client.State.Level.UpdateTickInformation(setTimePacket.WorldAge, setTimePacket.TimeOfDay,
                    setTimePacket.TimeOfDayIncreasing);
                break;
            }
            case LoginPacket loginPacket:
            {
                if (client.State.LocalPlayer.Entity is not { } entity) break;
                entity.EntityId = loginPacket.EntityId;
                break;
            }
            case AddEntityPacket addEntityPacket:
            {
                const int playerEntityType = 147;
                if (addEntityPacket.Type is not playerEntityType) break;
                await client.State.Level.AddEntityAsync(addEntityPacket.EntityUuid, addEntityPacket.EntityId, addEntityPacket.Position);
                break;
            }
            case RemoveEntitiesPacket removeEntitiesPacket:
            {
                var entities = client.State.Level.GetAllEntityIds().Where(x => removeEntitiesPacket.Entities.Contains(x));
                foreach (var entity in entities)
                {
                    await client.State.Level.RemoveEntityAsync(entity);
                }

                break;
            }
            case DisconnectPacket disconnectPacket:
            {
                var translateLookup = disconnectPacket.DisconnectReason.FindTag<NbtString>("translate")?.Value;
                var messages = disconnectPacket.DisconnectReason.FindTags<NbtString>(null).Reverse().Select(x => x.Value);
                Console.WriteLine($"Disconnected from Server for: ({translateLookup}) {string.Join(" ", messages)}");
                break;
            }
            case SystemChatPacket systemChatPacket:
            {
                var translateLookup = systemChatPacket.Tags.FindTag<NbtString>("translate")?.Value;
                var texts = systemChatPacket.Tags.FindTags<NbtString>("text").Reverse().Select(x => x.Value);
                Console.WriteLine($"System Message: ({translateLookup ?? "<NULL>"}) {string.Join(" ", texts)}");
                break;
            }
            case ContainerSetContentPacket containerSetContentPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.Inventory = containerSetContentPacket.SlotData
                    .Select((x, i) => new { Index = (short)i, Slot = x })
                    .ToDictionary(x => x.Index, x => x.Slot);
                entity.BlockPlaceSequence = containerSetContentPacket.StateId + 1;
                break;
            }
            case ContainerSetSlotPacket containerSetSlotPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.Inventory[containerSetSlotPacket.SlotToUpdate] = containerSetSlotPacket.Slot;
                entity.BlockPlaceSequence = containerSetSlotPacket.StateId + 1;
                break;
            }
            case BlockChangedAcknowledgementPacket: blockChangedAcknowledgementPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.HeldItem.ItemCount -= 1;
                if (entity.HeldItem.ItemCount <= 0) entity.Inventory[entity.HeldSlotWithOffset] = new Slot();
                break;
            }
            case PlayerChatPacket playerChatPacket:
            {
                await client.HandleChatMessageAsync(playerChatPacket.Header.Uuid, playerChatPacket.Body.Message);
                break;
            }
            case SetHeldSlotPacket setHeldSlotPacket:
            {
                if (client.State.LocalPlayer.HasEntity) client.State.LocalPlayer.Entity.HeldSlot = setHeldSlotPacket.HeldSlot;
                break;
            }
            case PlayerPositionPacket playerPositionPacket: // TODO: Will fire on join or when moving too quickly or other teleport.
            {
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                if (!_playerLoaded)
                {
                    await client.SendPacketAsync(new PlayerLoadedPacket());
                    _playerLoaded = true;
                }

                if (!client.State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
                client.State.LocalPlayer.Entity.Position = playerPositionPacket.Position;
                client.State.LocalPlayer.Entity.Velocity = playerPositionPacket.Velocity;
                client.State.LocalPlayer.Entity.YawPitch = playerPositionPacket.YawPitch;
                await client.SendPacketAsync(client.Move(playerPositionPacket.Position.X, playerPositionPacket.Position.Y,
                    playerPositionPacket.Position.Z, playerPositionPacket.YawPitch.X, playerPositionPacket.YawPitch.Y));
                break;
            }
            case KeepAlivePacket keepAlivePacket:
            {
                await client.SendPacketAsync(new Packets.Play.Serverbound.KeepAlivePacket { Payload = keepAlivePacket.Payload });
                break;
            }
            case PlayerCombatKillPacket playerCombatKillPacket:
            {
                Console.WriteLine($"{playerCombatKillPacket.PlayerId} died for {playerCombatKillPacket.DeathMessage}");
                break;
            }
            case SetHealthPacket setHealthPacket:
            {
                if (setHealthPacket.Health <= 0)
                {
                    await client.SendPacketAsync(new ClientCommandPacket { ActionId = ClientCommandPacket.Action.PerformRespawn });
                }

                break;
            }
            case EntityPositionSyncPacket entityPositionSyncPacket:
            {
                client.SetPosition(entityPositionSyncPacket.EntityId, entityPositionSyncPacket.Position, false);
                break;
            }
            case MoveEntityPositionRotationPacket moveEntityPositionRotationPacket:
            {
                client.SetPosition(moveEntityPositionRotationPacket.EntityId, moveEntityPositionRotationPacket.Delta);
                break;
            }
            case MoveEntityPositionPacket moveEntityPositionPacket:
            {
                client.SetPosition(moveEntityPositionPacket.EntityId, moveEntityPositionPacket.Delta);
                break;
            }
            case PingPacket pingPacket:
            {
                await client.SendPacketAsync(new PongPacket { Payload = pingPacket.Id });
                break;
            }
            case PongResponsePacket pongResponsePacket:
            {
                await client.SendPacketAsync(
                    new ChatPacket($"Ping: {TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds() - pongResponsePacket.Payload}ms"));
                break;
            }
            case LevelChunkWithLightPacket levelChunkWithLightPacket:
            {
                // TODO: Check we're updating this correctly.
                var (chunkX, chunkZ) = (levelChunkWithLightPacket.ChunkX, levelChunkWithLightPacket.ChunkZ);
                client.State.Level.Chunks.AddOrUpdate((chunkX, chunkZ), levelChunkWithLightPacket.Chunk,
                    (_, _) => levelChunkWithLightPacket.Chunk);
                break;
            }
            case ForgetLevelChunkPacket forgetLevelChunkPacket:
            {
                var (chunkX, chunkZ) = (forgetLevelChunkPacket.ChunkX, forgetLevelChunkPacket.ChunkZ);
                client.State.Level.Chunks.TryRemove((chunkX, chunkZ), out _);
                break;
            }
            case PlayerInfoUpdatePacket playerInfoUpdatePacket:
            {
                foreach (var info in playerInfoUpdatePacket.PlayerInfos)
                {
                    var player = client.State.Level.GetPlayerByUuid(info.Uuid);
                    foreach (var action in info.Actions)
                    {
                        switch (action)
                        {
                            // 'AddPlayer' should always come in first.
                            case PlayerInfoUpdatePacket.AddPlayer addPlayer:
                            {
                                player = await client.State.Level.AddPlayerAsync(info.Uuid, addPlayer.Username);
                                player.Properties = addPlayer.Properties.ToList();
                                break;
                            }
                            case PlayerInfoUpdatePacket.UpdateGameMode updateGameMode:
                            {
                                if (player is null) break;
                                player.GameMode = updateGameMode.GameMode;
                                break;
                            }
                            case PlayerInfoUpdatePacket.UpdateLatency updateLatency:
                            {
                                if (player is null) break;
                                player.Latency = updateLatency.Latency;
                                break;
                            }
                        }
                    }
                }

                break;
            }
            case PlayerInfoRemovePacket playerInfoRemovePacket:
            {
                foreach (var uuid in playerInfoRemovePacket.Uuids)
                {
                    await client.State.Level.RemovePlayerAsync(uuid);
                }

                break;
            }
        }
    }
}
