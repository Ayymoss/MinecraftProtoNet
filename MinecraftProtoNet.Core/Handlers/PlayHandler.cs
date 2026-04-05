using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Core.Handlers;

[HandlesPacket(typeof(LoginPacket))]
[HandlesPacket(typeof(PlayerPositionPacket))]
public class PlayHandler(ILogger<PlayHandler> logger, IGameLoop gameLoop) : IPacketHandler
{

    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(PlayHandler));


    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginPacket loginPacket:
            {
                // Store server settings
                client.State.ServerSettings.EnforcesSecureChat = loginPacket.EnforcesSecureChat;
                client.State.ServerSettings.IsHardcore = loginPacket.IsHardcore;
                client.State.ServerSettings.ViewDistance = loginPacket.ViewDistance;
                client.State.ServerSettings.SimulationDistance = loginPacket.SimulationDistance;

                if (client.State.LocalPlayer.Entity is not { } entity) break;
                entity.EntityId = loginPacket.EntityId;

                // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:474-535
                // Vanilla's handleLogin sends NO packets here. ChatSessionUpdate is prepared
                // asynchronously (prepareKeyPair) and sent later. Channel registration
                // (minecraft:register) is done in Configuration, not Play for 1.20.2+.
                //
                // IMPORTANT: Do NOT send ChatSessionUpdate here. In vanilla, it's sent async
                // (after prepareKeyPair completes). Sending it immediately can hit a Velocity
                // proxy race condition where getConnectedServer() is still null, causing ALL
                // subsequent client packets (including AcceptTeleportation) to be silently dropped.
                // ChatSessionUpdate is deferred to after the first teleport confirmation below.
                break;
            }

            case PlayerPositionPacket playerPositionPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
                var entity = client.State.LocalPlayer.Entity;

                // --- Use StateLock to ensure atomicity of position update and confirmation ---
                // This prevents the physics loop from moving the entity BETWEEN the update and confirmation.
                await entity.StateLock.WaitAsync();
                try
                {
                    // --- Apply position/velocity/rotation from packet FIRST ---
                    // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:744-753
                    // Java order: setValuesFromPositionPacket() → send AcceptTeleportation → send PosRot confirmation
                    var flags = playerPositionPacket.Flags;

                    // Update position
                    var posX = flags.HasFlag(PlayerPositionPacket.PositionFlags.X) ? entity.Position.X + playerPositionPacket.Position.X : playerPositionPacket.Position.X;
                    var posY = flags.HasFlag(PlayerPositionPacket.PositionFlags.Y) ? entity.Position.Y + playerPositionPacket.Position.Y : playerPositionPacket.Position.Y;
                    var posZ = flags.HasFlag(PlayerPositionPacket.PositionFlags.Z) ? entity.Position.Z + playerPositionPacket.Position.Z : playerPositionPacket.Position.Z;
                    entity.Position = new Vector3<double>(posX, posY, posZ);

                    // Update velocity 
                    var velX = flags.HasFlag(PlayerPositionPacket.PositionFlags.Delta_X) ? entity.Velocity.X + playerPositionPacket.Velocity.X : playerPositionPacket.Velocity.X;
                    var velY = flags.HasFlag(PlayerPositionPacket.PositionFlags.Delta_Y) ? entity.Velocity.Y + playerPositionPacket.Velocity.Y : playerPositionPacket.Velocity.Y;
                    var velZ = flags.HasFlag(PlayerPositionPacket.PositionFlags.Delta_Z) ? entity.Velocity.Z + playerPositionPacket.Velocity.Z : playerPositionPacket.Velocity.Z;
                    entity.Velocity = new Vector3<double>(velX, velY, velZ);

                    // Update Rotation
                    var yaw = flags.HasFlag(PlayerPositionPacket.PositionFlags.Y_ROT) ? entity.YawPitch.X + playerPositionPacket.YawPitch.X : playerPositionPacket.YawPitch.X;
                    var pitch = flags.HasFlag(PlayerPositionPacket.PositionFlags.X_ROT) ? entity.YawPitch.Y + playerPositionPacket.YawPitch.Y : playerPositionPacket.YawPitch.Y;
                    entity.YawPitch = new Vector2<float>(yaw, pitch);

                    entity.IsOnGround = false; // Reset on-ground state until next physics tick

                    logger.LogDebug("Applied teleport: TeleportId={TeleportId}, Position={Position}, Velocity={Velocity}, Flags={Flags}",
                        playerPositionPacket.TeleportId, entity.Position, entity.Velocity, flags);

                    // --- Send AcceptTeleportation THEN position confirmation (matching Java exactly) ---
                    // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:751-752
                    // Java sends BOTH packets immediately in the handler:
                    //   this.connection.send(new ServerboundAcceptTeleportationPacket(packet.id()));
                    //   this.connection.send(new ServerboundMovePlayerPacket.PosRot(player.getX(), player.getY(), player.getZ(), 
                    //                        player.getYRot(), player.getXRot(), false, false));
                    await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                    await client.SendPacketAsync(new MovePlayerPositionRotationPacket
                    {
                        X = entity.Position.X,
                        Y = entity.Position.Y,
                        Z = entity.Position.Z,
                        Yaw = entity.YawPitch.X,
                        Pitch = entity.YawPitch.Y,
                        Flags = Enums.MovementFlags.None // Java passes false, false (not on ground, no horizontal collision)
                    });

                    // Sync "last sent" tracking to the teleported position so SendPositionAsync
                    // doesn't re-send a duplicate or stale position on the next tick.
                    entity.LastSentPosition = entity.Position;
                    entity.LastSentYawPitch = entity.YawPitch;
                    entity.LastSentOnGround = entity.IsOnGround;
                    entity.LastSentHorizontalCollision = false;
                    entity.PositionReminder = 0;

                    // Mark teleport as pending so physics skips the next tick
                    // (the entity must remain at the exact teleported position for one tick)
                    entity.HasPendingTeleport = true;
                    entity.TeleportYawPitch = playerPositionPacket.YawPitch;
                }
                finally
                {
                    entity.StateLock.Release();
                }

                if (!gameLoop.IsRunning)
                {
                    // Start the game loop (physics tick loop)
                    // Note: Baritone hook should already be attached via BaritoneGameLoopHook singleton
                    // PlayerLoadedPacket is deferred to ChunkHandler — vanilla only sends it
                    // after enough chunks are loaded (levelLoadTracker.isLevelReady()).
                    // Sending it prematurely can cause proxy servers to skip chunk delivery.
                    gameLoop.Start(client);

                    // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:523-535
                    // Vanilla sends ChatSessionUpdate asynchronously after prepareKeyPair completes.
                    // We defer it to give ViaVersion plugin channels (vv:server_details) time to arrive.
                    // ViaVersion sets EnforcesSecureChat:true even when the backend can't handle
                    // ChatSessionUpdate — sending it causes a forced disconnect. By waiting ~2 seconds
                    // after the first teleport, the vv:server_details channel will have arrived and set
                    // HasViaVersion, allowing us to safely skip ChatSessionUpdate on proxied servers.
                    if (client.State.ServerSettings.EnforcesSecureChat)
                    {
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(2000);
                            if (client.State.ServerSettings.HasViaVersion)
                            {
                                logger.LogInformation("Skipping ChatSessionUpdate — ViaVersion detected, backend may not support it");
                                return;
                            }
                            try
                            {
                                await client.SendChatSessionUpdate();
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to send ChatSessionUpdate — server may not support signed chat");
                            }
                        });
                    }
                }

                // Notify listeners (pathfinding) that server sent a teleport packet
                entity.NotifyServerTeleport(entity.Position);
                break;
            }
        }
    }
}
