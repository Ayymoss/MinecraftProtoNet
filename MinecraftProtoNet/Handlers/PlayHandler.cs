using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags.Primitive;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

[HandlesPacket(typeof(SetTimePacket))]
[HandlesPacket(typeof(BlockUpdatePacket))]
[HandlesPacket(typeof(LoginPacket))]
[HandlesPacket(typeof(AddEntityPacket))]
[HandlesPacket(typeof(RemoveEntitiesPacket))]
[HandlesPacket(typeof(DisconnectPacket))]
[HandlesPacket(typeof(SystemChatPacket))]
[HandlesPacket(typeof(ContainerSetContentPacket))]
[HandlesPacket(typeof(HurtAnimationPacket))]
[HandlesPacket(typeof(ContainerSetSlotPacket))]
[HandlesPacket(typeof(BlockChangedAcknowledgementPacket))]
[HandlesPacket(typeof(PlayerChatPacket))]
[HandlesPacket(typeof(SetHeldSlotPacket))]
[HandlesPacket(typeof(PlayerPositionPacket))]
[HandlesPacket(typeof(KeepAlivePacket))]
[HandlesPacket(typeof(PlayerCombatKillPacket))]
[HandlesPacket(typeof(SetHealthPacket))]
[HandlesPacket(typeof(EntityPositionSyncPacket))]
[HandlesPacket(typeof(MoveEntityPositionRotationPacket))]
[HandlesPacket(typeof(MoveEntityPositionPacket))]
[HandlesPacket(typeof(SetEntityMotionPacket))]
[HandlesPacket(typeof(PingPacket))]
[HandlesPacket(typeof(PongResponsePacket))]
[HandlesPacket(typeof(LevelChunkWithLightPacket))]
[HandlesPacket(typeof(ForgetLevelChunkPacket))]
[HandlesPacket(typeof(PlayerInfoUpdatePacket))]
[HandlesPacket(typeof(PlayerInfoRemovePacket))]
[HandlesPacket(typeof(SetEntityDataPacket))]
[HandlesPacket(typeof(UpdateAttributesPacket))]
[HandlesPacket(typeof(LevelEventPacket))]
[HandlesPacket(typeof(SoundPacket))]
[HandlesPacket(typeof(SetEquipmentPacket))]
[HandlesPacket(typeof(TickingStatePacket))]
[HandlesPacket(typeof(TickingStepPacket))]
[HandlesPacket(typeof(ServerDataPacket))]
[HandlesPacket(typeof(SetChunkCacheCenterPacket))]
[HandlesPacket(typeof(TrackedWaypointPacket))]
[HandlesPacket(typeof(ChunkBatchFinishedPacket))]
[HandlesPacket(typeof(TakeItemEntityPacket))]
public class PlayHandler : IPacketHandler
{
    private bool _playerLoaded;

    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(PlayHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case SetTimePacket setTimePacket:
            {
                client.State.Level.UpdateTickInformation(setTimePacket.WorldAge, setTimePacket.TimeOfDay,
                    setTimePacket.TimeOfDayIncreasing);
                break;
            }
            case BlockUpdatePacket blockUpdatePacket:
            {
                client.State.Level.HandleBlockUpdate(blockUpdatePacket.Position, blockUpdatePacket.BlockId);
                break;
            }
            case LoginPacket loginPacket:
            {
                if (client.AuthResult.ChatSession is not null)
                {
                    await client.SendPacketAsync(new ChatSessionUpdatePacket
                    {
                        SessionId = client.AuthResult.ChatSession.ChatContext.ChatSessionGuid,
                        ExpiresAt = client.AuthResult.ChatSession.ExpiresAtEpochMs,
                        PublicKey = client.AuthResult.ChatSession.PublicKeyDer,
                        KeySignature = client.AuthResult.ChatSession.MojangSignature
                    });
                }

                if (client.State.LocalPlayer.Entity is not { } entity) break;
                entity.EntityId = loginPacket.EntityId;
                break;
            }
            case AddEntityPacket addEntityPacket:
            {
                const int playerEntityType = 148;
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
                break;
            }
            case HurtAnimationPacket hurtAnimationPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.IsHurtFromYaw = hurtAnimationPacket.Yaw;
                break;
            }
            case ContainerSetSlotPacket containerSetSlotPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.Inventory[containerSetSlotPacket.SlotToUpdate] = containerSetSlotPacket.Slot;
                break;
            }
            case BlockChangedAcknowledgementPacket blockChangedAcknowledgementPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;
                entity.HeldItem.ItemCount -= 1;
                if (entity.HeldItem.ItemCount <= 0) entity.Inventory[entity.HeldSlotWithOffset] = new Slot();
                break;
            }
            case PlayerChatPacket playerChatPacket:
            {
                var signatureBytes = playerChatPacket.Header.MessageSignature;

                if (signatureBytes is not null)
                {
                    ChatSigning.ChatMessageReceived(client.AuthResult, signatureBytes);
                }

                _ = Task.Run(async () => await client.HandleChatMessageAsync(playerChatPacket.Header.Uuid, playerChatPacket.Body.Message));
                break;
            }
            case SetHeldSlotPacket setHeldSlotPacket:
            {
                if (client.State.LocalPlayer.HasEntity) client.State.LocalPlayer.Entity.HeldSlot = setHeldSlotPacket.HeldSlot;
                break;
            }
            case PlayerPositionPacket playerPositionPacket:
            {
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                if (!_playerLoaded)
                {
                    await client.SendPacketAsync(new PlayerLoadedPacket());

                    var physicsThread = new Thread(async () =>
                    {
                        var stopwatch = new System.Diagnostics.Stopwatch();

                        while (true)
                        {
                            stopwatch.Restart();
                            try
                            {
                                await client.PhysicsTickAsync();
                                client.State.Level.IncrementClientTickCounter();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error in physics tick: {ex}");
                            }

                            stopwatch.Stop();

                            var targetDelayMs = client.State.Level.TickInterval;
                            var processingTimeMs = stopwatch.ElapsedMilliseconds;
                            targetDelayMs = Math.Max(1, Math.Min(1000, targetDelayMs));

                            if (processingTimeMs < targetDelayMs)
                            {
                                var remainingDelayMs = targetDelayMs - processingTimeMs;
                                await Task.Delay(TimeSpan.FromMilliseconds(remainingDelayMs));
                            }
                            else
                            {
                                await Task.Yield();
                            }

                            await client.SendPacketAsync(new ClientTickEndPacket());
                        }
                    }) { Name = "Local Entity Physics", IsBackground = true };
                    physicsThread.Start();

                    _playerLoaded = true;
                }

                // --- Position update ---
                if (!client.State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
                client.State.LocalPlayer.Entity.Position = playerPositionPacket.Position;
                //client.State.LocalPlayer.Entity.Velocity = playerPositionPacket.Velocity;
                client.State.LocalPlayer.Entity.YawPitch = playerPositionPacket.YawPitch;
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
                if (!client.State.LocalPlayer.HasEntity) break;
                var entity = client.State.LocalPlayer.Entity;

                entity.Health = setHealthPacket.Health;
                entity.Hunger = setHealthPacket.Food;
                entity.HungerSaturation = setHealthPacket.FoodSaturation;

                if (setHealthPacket.Health <= 0)
                {
                    await client.SendPacketAsync(new ClientCommandPacket { ActionId = ClientCommandPacket.Action.PerformRespawn });
                }

                break;
            }
            case EntityPositionSyncPacket entityPositionSyncPacket:
            {
                await client.State.Level.SetPositionAsync(entityPositionSyncPacket.EntityId, entityPositionSyncPacket.Position,
                    entityPositionSyncPacket.Velocity, entityPositionSyncPacket.YawPitch, entityPositionSyncPacket.OnGround);
                break;
            }
            case MoveEntityPositionRotationPacket moveEntityPositionRotationPacket:
            {
                await client.State.Level.UpdatePositionAsync(moveEntityPositionRotationPacket.EntityId,
                    moveEntityPositionRotationPacket.Delta, moveEntityPositionRotationPacket.OnGround);
                break;
            }
            case MoveEntityPositionPacket moveEntityPositionPacket:
            {
                await client.State.Level.UpdatePositionAsync(moveEntityPositionPacket.EntityId, moveEntityPositionPacket.Delta,
                    moveEntityPositionPacket.OnGround);
                break;
            }
            case SetEntityMotionPacket setEntityMotionPacket:
            {
                // @lassipulkkinen -> Even though the server will tell you the velocity of entities other than the local player (don't ask me why),
                // you're not supposed to actually use it for anything. There are some exceptions (projectiles and item entities are
                // simulated on the client and the server will send updates less often), but other than that the client just lerps the
                // positions that are sent every 3 ticks
                //await client.State.Level.SetVelocityAsync(setEntityMotionPacket.EntityId, setEntityMotionPacket.Velocity);
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
            case ChunkBatchFinishedPacket:
            {
                // Respond to chunk batch finished to acknowledge receipt and keep chunks flowing
                await client.SendPacketAsync(new ChunkBatchReceivedPacket { DesiredChunksPerTick = 7.0f });
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
