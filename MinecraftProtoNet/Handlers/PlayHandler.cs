using System.Numerics;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler : IPacketHandler
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

    private int _clientId;
    private Vector3 _position;
    private bool _lock;

    public async Task HandleAsync(Packet packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginPacket loginPacket:
                _clientId = loginPacket.Login.EntityId;
                Console.WriteLine($"Login Packet Received from {loginPacket.Login.EntityId}");
                break;
            case DisconnectPacket disconnectPacket:
                Console.WriteLine($"Disconnected from Server for: {disconnectPacket.Payload.DisconnectReason}");
                break;
            case PlayerPositionPacket playerPositionPacket: // TODO: Will fire on join or when moving too quickly or other teleport.
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                _position = playerPositionPacket.Position;
                if (_lock)
                {
                    await client.SendPacketAsync(Move(playerPositionPacket.Position.X, playerPositionPacket.Position.Y,
                        playerPositionPacket.Position.Z));
                    return;
                }

                _ = Task.Run(async () =>
                {
                    _lock = true;
                    while (true)
                    {
                        await client.SendPacketAsync(Move(_position.X, _position.Y + 1f, _position.Z));
                        await Task.Delay(1000);
                    }
                });
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
                Console.WriteLine($"RECEIVED MOVE FOR {moveEntityPositionRotationPacket.Payload.EntityId} - STORED CLIENTID: {_clientId}");

                //if (_clientId == moveEntityPositionRotationPacket.Payload.EntityId)
                //{
                //    if (moveEntityPositionRotationPacket.Payload.OnGround) break;
                //    await client.SendPacketAsync(Move(moveEntityPositionRotationPacket.Payload.DeltaX,
                //        moveEntityPositionRotationPacket.Payload.DeltaY, moveEntityPositionRotationPacket.Payload.DeltaZ));
                //}

                break;
            case MoveEntityPositionPacket moveEntityPositionPacket:
                Console.WriteLine($"RECEIVED -> {moveEntityPositionPacket.Payload.EntityId} -> " +
                                  $"X: {moveEntityPositionPacket.Payload.DeltaX}, " +
                                  $"Y: {moveEntityPositionPacket.Payload.DeltaY}, " +
                                  $"Z: {moveEntityPositionPacket.Payload.DeltaZ}, " +
                                  $"G: {moveEntityPositionPacket.Payload.OnGround}");
                //if (_clientId == moveEntityPositionPacket.Payload.EntityId)
                //{
                //    if (moveEntityPositionPacket.Payload.OnGround) break;
                //    await client.SendPacketAsync(Move(moveEntityPositionPacket.Payload.DeltaX,
                //        moveEntityPositionPacket.Payload.DeltaY, moveEntityPositionPacket.Payload.DeltaZ));
                //}
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
        _position.X = (float)result.Payload.X;
        _position.Y = (float)result.Payload.Y;
        _position.Z = (float)result.Payload.Z;
        Console.WriteLine($"SENDING -> " +
                          $"X: {result.Payload.X}, " +
                          $"Y: {result.Payload.Y}, " +
                          $"Z: {result.Payload.Z}, ");
        return result;
    }
}
