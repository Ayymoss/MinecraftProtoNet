using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler(MinecraftClientState clientState) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
    [
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
    ];

    public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginPacket loginPacket:
                clientState.EntityId = loginPacket.EntityId;
                Console.WriteLine($"Login Packet Received from {loginPacket.EntityId}");
                break;
            case DisconnectPacket disconnectPacket:
                Console.WriteLine($"Disconnected from Server for: {disconnectPacket.DisconnectReason}");
                break;
            case PlayerPositionPacket playerPositionPacket: // TODO: Will fire on join or when moving too quickly or other teleport.
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });

                // Accept position correction
                clientState.Position.Vector3ToVector3F(playerPositionPacket.Position);
                clientState.Velocity.Vector3ToVector3F(playerPositionPacket.Velocity);
                clientState.Rotation.Vector2ToVector2F(playerPositionPacket.Rotation);
                await client.SendPacketAsync(Move(playerPositionPacket.Position.X, playerPositionPacket.Position.Y,
                    playerPositionPacket.Position.Z));
                break;
            case KeepAlivePacket keepAlivePacket:
                await client.SendPacketAsync(new Packets.Play.Serverbound.KeepAlivePacket { Payload = keepAlivePacket.Payload });
                break;
            case PlayerCombatKillPacket playerCombatKillPacket:
                Console.WriteLine($"{playerCombatKillPacket.PlayerId} died for {playerCombatKillPacket.DeathMessage}");
                break;
            case SetHealthPacket setHealthPacket:
                if (setHealthPacket.Health <= 0)
                {
                    await client.SendPacketAsync(new ClientCommandPacket { ActionId = ClientCommandPacket.Action.PerformRespawn });
                }

                break;
            case MoveEntityPositionRotationPacket moveEntityPositionRotationPacket:
                break;
            case MoveEntityPositionPacket moveEntityPositionPacket:
                Console.WriteLine($"Entity {moveEntityPositionPacket.EntityId} on ground? {moveEntityPositionPacket.OnGround}");
                break;
        }
    }

    public MovePlayerPositionRotationPacket Move(float x, float y, float z)
    {
        var result = new MovePlayerPositionRotationPacket
        {
            Payload = new MovePlayerPositionRotationPacket.MovePlayerPositionRotation
            {
                X = x,
                Y = y,
                Z = z,
                Yaw = 0,
                Pitch = 0,
                MovementFlags = MovePlayerPositionRotationPacket.MovementFlags.None
            }
        };
        clientState.Position.X = (float)result.Payload.X;
        clientState.Position.Y = (float)result.Payload.Y;
        clientState.Position.Z = (float)result.Payload.Z;
        clientState.Rotation.X = result.Payload.Yaw;
        Console.WriteLine($"SENDING -> " +
                          $"X: {result.Payload.X}, " +
                          $"Y: {result.Payload.Y}, " +
                          $"Z: {result.Payload.Z}, ");
        return result;
    }
}
