using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
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

[HandlesPacket(typeof(LoginPacket))]
[HandlesPacket(typeof(PlayerPositionPacket))]
public class PlayHandler : IPacketHandler
{
    private bool _playerLoaded;
    private readonly ILogger<PlayHandler> _logger;
    private readonly IGameLoop _gameLoop;

    public PlayHandler(ILogger<PlayHandler> logger, IGameLoop gameLoop)
    {
        _logger = logger;
        _gameLoop = gameLoop;
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

            case PlayerPositionPacket playerPositionPacket:
            {
                if (!client.State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
                var entity = client.State.LocalPlayer.Entity;

                entity.HasPendingTeleport = true;
                entity.TeleportYawPitch = playerPositionPacket.YawPitch;

                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                if (!_playerLoaded)
                {
                    await client.SendPacketAsync(new PlayerLoadedPacket());

                    // Start the game loop (physics tick loop)
                    _gameLoop.Start(client);

                    _playerLoaded = true;
                }

                // --- Position update ---
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

                _logger.LogDebug("Applied teleport: TeleportId={TeleportId}, Position={Position}, Velocity={Velocity}, Flags={Flags}",
                    playerPositionPacket.TeleportId, entity.Position, entity.Velocity, flags);
                entity.IsOnGround = false; // Reset on-ground state until next physics tick
                
                // Notify listeners (pathfinding) that server sent a teleport packet
                entity.NotifyServerTeleport(entity.Position);
                break;
            }
        }
    }
}
