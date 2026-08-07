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
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.NBT;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Handlers;

[HandlesPacket(typeof(LoginPacket))]
[HandlesPacket(typeof(RespawnPacket))]
[HandlesPacket(typeof(PlayerPositionPacket))]
public class PlayHandler(ILogger<PlayHandler> logger, IGameLoop gameLoop) : IPacketHandler
{
    /// <summary>
    /// Opt-in setback diagnostic (MCPROTO_SETBACK_DIAG=1). A server position packet is only a *rejection* if it
    /// actually moves us; many servers echo the player's own position back periodically as an anti-cheat
    /// re-sync, which is a no-op correction. Counting packets cannot tell those apart — only the delta can.
    /// </summary>
    private static readonly bool SetbackDiagEnabled =
        Environment.GetEnvironmentVariable("MCPROTO_SETBACK_DIAG") == "1";

    /// <summary>
    /// Renders the block column around a position (feet, and the three blocks below) for the setback diag,
    /// marking whether each one actually contributes a collision shape.
    /// </summary>
    private static string DescribeColumn(IMinecraftClient client, Models.Core.Vector3<double> position)
    {
        var bx = (int)Math.Floor(position.X);
        var bz = (int)Math.Floor(position.Z);
        var feetY = (int)Math.Floor(position.Y);

        var parts = new List<string>();
        for (var y = feetY; y >= feetY - 3; y--)
        {
            var state = client.State.Level.GetBlockAt(bx, y, bz);
            if (state is null)
            {
                parts.Add($"y{y}=<null>");
                continue;
            }

            var shape = Physics.BlockShapeRegistry.Shared.GetShape(state);
            var props = state.Properties.Count == 0
                ? "{}"
                : "{" + string.Join(",", state.Properties.Select(p => $"{p.Key}={p.Value}")) + "}";
            var top = shape.IsEmpty() ? "none" : shape.ToAABBs().Max(b => b.MaxY).ToString("F2");
            parts.Add($"y{y}={state.Name}#{state.Id}{props} top={top}");
        }

        return $"({position.X:F2},{position.Y:F2},{position.Z:F2}) " + string.Join(" ", parts);
    }

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

                // A Login packet means a new level — on a proxy network that is a switch to a different
                // backend, which has never heard from this client. Re-arm so ChunkHandler sends player_loaded
                // for this level too; without it the new server pins the player at spawn.
                // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:500
                client.State.ClientLoadedNotified = false;

                ApplyDimensionBounds(client, loginPacket.DimensionType, loginPacket.DimensionName);

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

            case RespawnPacket respawnPacket:
            {
                // Respawn also loads a level (death, dimension change, and on proxy networks some server
                // switches), so the new level must be acknowledged the same way a join is.
                // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:1180
                client.State.ClientLoadedNotified = false;
                ApplyDimensionBounds(client, respawnPacket.DimensionType, respawnPacket.DimensionName);
                logger.LogDebug("Respawn into {Dimension}; awaiting first chunk to re-send player_loaded",
                    respawnPacket.DimensionName);
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

                    var priorPosition = entity.Position;
                    var priorVelocity = entity.Velocity;
                    var priorOnGround = entity.IsOnGround;

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

                    // Deliberately NOT touching IsOnGround here.
                    // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:764-782
                    // setValuesFromPositionPacket sets position, delta movement and rotation — and nothing else.
                    // Clearing on-ground was a non-vanilla addition, and it is not merely redundant (PhysicsService
                    // recomputes it from collision every tick): servers that periodically re-sync a player's own
                    // position make it actively harmful. Hypixel echoes the player's position back every 5 ticks,
                    // so the bot was being declared airborne four times a second, losing ground acceleration and
                    // sprint each time, and crawled at roughly a third of walking speed.

                    logger.LogDebug("Applied teleport: TeleportId={TeleportId}, Position={Position}, Velocity={Velocity}, Flags={Flags}",
                        playerPositionPacket.TeleportId, entity.Position, entity.Velocity, flags);

                    if (SetbackDiagEnabled)
                    {
                        var i = entity.InputState.Current;
                        var held = string.Join("+", new[]
                        {
                            i.Forward ? "F" : null, i.Backward ? "B" : null,
                            i.Left ? "L" : null, i.Right ? "R" : null,
                            i.Jump ? "J" : null, i.Sprint ? "SPR" : null, i.Shift ? "SNK" : null
                        }.Where(s => s is not null));

                        var dx = entity.Position.X - priorPosition.X;
                        var dy = entity.Position.Y - priorPosition.Y;
                        var dz = entity.Position.Z - priorPosition.Z;
                        var horizontal = Math.Sqrt(dx * dx + dz * dz);
                        logger.LogInformation(
                            "[SetbackDiag] tick={Tick} id={TeleportId} flags={Flags} dx={Dx:F4} dy={Dy:F4} dz={Dz:F4} " +
                            "dh={Dh:F4} from=({Fx:F3},{Fy:F3},{Fz:F3}) to=({Tx:F3},{Ty:F3},{Tz:F3}) " +
                            "priorVel=({Vx:F4},{Vy:F4},{Vz:F4}) newVel=({Nx:F4},{Ny:F4},{Nz:F4}) onGround={OnGround} " +
                            "input=[{Held}] moveSpeed={MoveSpeed:F4}",
                            client.State.Level.ClientTickCounter, playerPositionPacket.TeleportId, flags,
                            dx, dy, dz, horizontal,
                            priorPosition.X, priorPosition.Y, priorPosition.Z,
                            entity.Position.X, entity.Position.Y, entity.Position.Z,
                            priorVelocity.X, priorVelocity.Y, priorVelocity.Z,
                            entity.Velocity.X, entity.Velocity.Y, entity.Velocity.Z,
                            priorOnGround, held, entity.MovementSpeed);

                        // When the server puts us HIGHER than we are, it found a surface our collision did
                        // not. Dump the block column at both positions so that is a fact, not an inference.
                        if (dy > 0.05)
                        {
                            logger.LogInformation(
                                "[SetbackDiag]   ours: {OursCol} | server: {ServerCol}",
                                DescribeColumn(client, priorPosition), DescribeColumn(client, entity.Position));
                        }
                    }

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

                    // No "pending teleport" flag: vanilla resumes normal physics on the very next tick.
                    // See the note in PhysicsService.PhysicsTickAsync.
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

    /// <summary>
    /// Resolves the level's vertical bounds from the dimension_type registry and applies them to chunk decoding.
    ///
    /// Chunk sections arrive as a bare run of sections from the bottom of the world up, so the world's minimum
    /// Y is what maps a block Y onto a section. It had been hardcoded to the vanilla overworld's -64, which is
    /// correct on most servers and silently wrong elsewhere: in a dimension starting at Y=0 every block lookup
    /// is displaced by 64 blocks, so the ground reads as air, the player never lands, and the server
    /// rubber-bands them back several times a second. That looks exactly like a pathfinding or anti-cheat
    /// problem and is neither.
    ///
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/dimension/DimensionType.java
    /// (min_y / height are fields of the registry entry, not constants).
    /// </summary>
    private void ApplyDimensionBounds(IMinecraftClient client, int dimensionTypeId, string dimensionName)
    {
        // The packet carries an index into the ordered dimension_type registry sent during configuration.
        if (!client.State.RegistryKeyOrder.TryGetValue("minecraft:dimension_type", out var orderedKeys)
            || dimensionTypeId < 0 || dimensionTypeId >= orderedKeys.Count
            || !client.State.Registry.TryGetValue("minecraft:dimension_type", out var entries)
            || !entries.TryGetValue(orderedKeys[dimensionTypeId], out var entry))
        {
            logger.LogWarning(
                "Dimension type {Id} ({Name}) not found in the registry; keeping bounds minY={MinY} height={Height}",
                dimensionTypeId, dimensionName, client.State.Level.DimensionType.MinY, client.State.Level.DimensionType.Height);
            return;
        }

        var minY = entry.FindTag<NbtInt>("min_y")?.Value;
        var height = entry.FindTag<NbtInt>("height")?.Value;
        if (minY is null || height is null)
        {
            logger.LogWarning("Dimension {Name} has no min_y/height; keeping the current bounds", dimensionName);
            return;
        }

        client.State.Level.DimensionType = new DimensionType(minY.Value, height.Value) { Name = dimensionName };
        Chunk.SetWorldBounds(minY.Value, height.Value);
        logger.LogInformation("Level bounds for {Name}: minY={MinY} height={Height} (minSection={MinSection}, sections={Sections})",
            dimensionName, minY.Value, height.Value, Chunk.MinSection, Chunk.SectionCount);
    }
}
