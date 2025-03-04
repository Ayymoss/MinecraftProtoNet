using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags.Primitive;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler(MinecraftClientState clientState) : IPacketHandler
{
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
    ];

    // TODO: Remove/move.
    private record Player(Guid Guid, int EntityId);

    private Dictionary<Player, Vector3D> _players = [];

    public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginPacket loginPacket:
            {
                clientState.EntityId = loginPacket.EntityId;
                Console.WriteLine($"Login Packet Received from {loginPacket.EntityId}");
                break;
            }
            case AddEntityPacket addEntityPacket:
            {
                if (addEntityPacket.Type is not 147) break;
                var player = _players.FirstOrDefault(x => x.Key.Guid == addEntityPacket.EntityUuid).Key;
                if (player is null)
                {
                    _players.Add(new Player(addEntityPacket.EntityUuid, addEntityPacket.EntityId), addEntityPacket.Position);
                }
                else
                {
                    _players[player] = addEntityPacket.Position;
                }

                break;
            }
            case RemoveEntitiesPacket removeEntitiesPacket:
            {
                var entities = _players.Where(x => removeEntitiesPacket.Entities.Contains(x.Key.EntityId));
                foreach (var entity in entities)
                {
                    _players.Remove(entity.Key);
                    Console.WriteLine($"Removed {entity.Key.EntityId}");
                }

                break;
            }
            case DisconnectPacket disconnectPacket:
            {
                Console.WriteLine(
                    $"Disconnected from Server for: {disconnectPacket.DisconnectReason.FindTag<NbtString>(null)?.Value ?? "FAILED TO PARSE"}");
                break;
            }
            case PlayerChatPacket playerChatPacket:
            {
                var playerName = playerChatPacket.Formatting.SenderName.FindTag<NbtString>(tagName: null)?.Value ?? "UNKNOWN";

                if (playerChatPacket.Body.Message.StartsWith("!say"))
                {
                    var message = playerChatPacket.Body.Message.Split(" ").Skip(1).ToArray();
                    await client.SendPacketAsync(new ChatPacket(string.Join(" ", message)));
                }

                if (playerChatPacket.Body.Message == "!pos")
                {
                    var playerPos = _players.Where(x => x.Key.Guid == playerChatPacket.Header.Uuid)
                        .Select(x => $"{playerName} -> {_players[x.Key].X:N2}, {_players[x.Key].Y:N2}, {_players[x.Key].Z:N2}")
                        .FirstOrDefault();
                    var message = playerPos ?? $"{playerName}'s location isn't known.";

                    await client.SendPacketAsync(new ChatPacket(message));
                }

                if (playerChatPacket.Body.Message == "!here")
                {
                    var playerPos = _players.Where(x => x.Key.Guid == playerChatPacket.Header.Uuid)
                        .Select(x => _players[x.Key]).FirstOrDefault();
                    if (playerPos != null)
                    {
                        InterpolateToCoordinates(client, playerPos);
                        await client.SendPacketAsync(new ChatPacket($"Moving to {playerPos.X:N2}, {playerPos.Y:N2}, {playerPos.Z:N2}"));
                    }
                    else
                    {
                        await client.SendPacketAsync(new ChatPacket($"{playerName}, your location isn't known."));
                    }
                }

                if (playerChatPacket.Body.Message.StartsWith("!goto"))
                {
                    var coords = playerChatPacket.Body.Message.Split(" ");
                    if (coords.Length == 4)
                    {
                        var x = float.Parse(coords[1]);
                        var y = float.Parse(coords[2]);
                        var z = float.Parse(coords[3]);
                        InterpolateToCoordinates(client, new Vector3D(x, y, z));
                        await client.SendPacketAsync(new ChatPacket($"Moving to {x:N2}, {y:N2}, {z:N2}"));
                    }
                }

                break;
            }
            case PlayerPositionPacket playerPositionPacket: // TODO: Will fire on join or when moving too quickly or other teleport.
            {
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                clientState.Position.Vector3ToVector3F(playerPositionPacket.Position);
                clientState.Velocity.Vector3ToVector3F(playerPositionPacket.Velocity);
                clientState.Rotation.Vector2ToVector2F(playerPositionPacket.Rotation);
                await client.SendPacketAsync(Move(playerPositionPacket.Position.X, playerPositionPacket.Position.Y,
                    playerPositionPacket.Position.Z));
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
            case MoveEntityPositionRotationPacket moveEntityPositionRotationPacket:
            {
                var (key1, newPos1) = _players.FirstOrDefault(x => x.Key.EntityId == moveEntityPositionRotationPacket.EntityId);
                if (key1 != null)
                {
                    newPos1.X += moveEntityPositionRotationPacket.DeltaX;
                    newPos1.Y += moveEntityPositionRotationPacket.DeltaY;
                    newPos1.Z += moveEntityPositionRotationPacket.DeltaZ;
                    _players[key1] = newPos1;
                }

                break;
            }
            case MoveEntityPositionPacket moveEntityPositionPacket:
            {
                var (key2, newPos2) = _players.FirstOrDefault(x => x.Key.EntityId == moveEntityPositionPacket.EntityId);
                if (key2 != null)
                {
                    newPos2.X += moveEntityPositionPacket.DeltaX;
                    newPos2.Y += moveEntityPositionPacket.DeltaY;
                    newPos2.Z += moveEntityPositionPacket.DeltaZ;
                    _players[key2] = newPos2;
                }

                break;
            }
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
        clientState.Position.X = result.Payload.X;
        clientState.Position.Y = result.Payload.Y;
        clientState.Position.Z = result.Payload.Z;
        clientState.Rotation.X = result.Payload.Yaw;
        Console.WriteLine($"SENDING -> " +
                          $"X: {result.Payload.X}, " +
                          $"Y: {result.Payload.Y}, " +
                          $"Z: {result.Payload.Z}, ");
        return result;
    }

    private static void InterpolateToCoordinates(IMinecraftClient client, Vector3D targetPosition)
    {
        const float baseSpeed = 0.4f;
        const float stoppingDistance = 0.5f;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var currentPosition = client.ClientState.Position;
                var direction = new Vector3D(
                    targetPosition.X - currentPosition.X,
                    targetPosition.Y - currentPosition.Y,
                    targetPosition.Z - currentPosition.Z);

                var distance = direction.Length();
                if (distance <= stoppingDistance) break;

                direction.X /= distance;
                direction.Y /= distance;
                direction.Z /= distance;

                var newPosition = currentPosition + direction * baseSpeed;
                var targetYaw = CalculateYawToTarget(currentPosition, targetPosition);
                client.ClientState.Rotation.X = NormalizeYaw(targetYaw);
                var pitchDegrees = client.ClientState.Rotation.Y;

                var result = new MovePlayerPositionRotationPacket
                {
                    Payload = new MovePlayerPositionRotationPacket.MovePlayerPositionRotation
                    {
                        X = newPosition.X,
                        Y = newPosition.Y,
                        Z = newPosition.Z,
                        Yaw = (float)client.ClientState.Rotation.X,
                        Pitch = (float)pitchDegrees,
                        MovementFlags = MovePlayerPositionRotationPacket.MovementFlags.None
                    }
                };

                client.ClientState.Position.X = result.Payload.X;
                client.ClientState.Position.Y = result.Payload.Y;
                client.ClientState.Position.Z = result.Payload.Z;

                await client.SendPacketAsync(result);
                await Task.Delay(20);
            }
        });
    }


    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360;
        switch (yaw)
        {
            case > 180:
                yaw -= 360;
                break;
            case <= -180:
                yaw += 360;
                break;
        }

        return yaw;
    }

    private static float CalculateYawToTarget(Vector3D currentPosition, Vector3D targetPosition)
    {
        var deltaX = targetPosition.X - currentPosition.X;
        var deltaZ = targetPosition.Z - currentPosition.Z;

        var yaw = (float)(Math.Atan2(-deltaX, deltaZ) * (180 / Math.PI));
        return NormalizeYaw(yaw);
    }
}
