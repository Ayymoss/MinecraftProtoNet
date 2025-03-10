using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags.Primitive;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.State.Base;
using KeepAlivePacket = MinecraftProtoNet.Packets.Play.Clientbound.KeepAlivePacket;

namespace MinecraftProtoNet.Handlers;

public class PlayHandler(ClientState clientState) : IPacketHandler
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
        (ProtocolState.Play, 0x37),
        (ProtocolState.Play, 0x38),
        (ProtocolState.Play, 0x5D),
    ];

    // TODO: Remove/move.
    private record Player(Guid Guid, int EntityId);

    private Dictionary<Player, Vector3<double>> _players = [];

    //Unknown packet for state Play and ID 3 (0x03)
    //Unknown packet for state Play and ID 9 (0x09)


    public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginPacket loginPacket:
            {
                clientState.Player.EntityId = loginPacket.EntityId;
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
                    // This will never be hit?
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

                if (playerChatPacket.Body.Message.StartsWith("!getblock"))
                {
                    var coords = playerChatPacket.Body.Message.Split(" ");
                    if (coords.Length == 4)
                    {
                        var x = int.Parse(coords[1]);
                        var y = int.Parse(coords[2]);
                        var z = int.Parse(coords[3]);
                        var block = clientState.Level.GetBlockAt(x, y, z);
                        var message = block != null
                            ? $"Block: ({block.Id}) {block.Name}"
                            : $"Block not found at {x}, {y}, {z}";
                        await client.SendPacketAsync(new ChatPacket(message));
                    }
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
                        InterpolateToCoordinates(client, new Vector3<double>(x, y, z));
                        await client.SendPacketAsync(new ChatPacket($"Moving to {x:N2}, {y:N2}, {z:N2}"));
                    }
                }

                if (playerChatPacket.Body.Message == "!ping")
                {
                    await client.SendPacketAsync(new Packets.Play.Serverbound.PingRequestPacket
                        { Payload = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds() });
                }

                break;
            }
            case PlayerPositionPacket playerPositionPacket: // TODO: Will fire on join or when moving too quickly or other teleport.
            {
                await client.SendPacketAsync(new AcceptTeleportationPacket { TeleportId = playerPositionPacket.TeleportId });
                clientState.Player.Position = playerPositionPacket.Position;
                clientState.Player.Velocity = playerPositionPacket.Velocity;
                clientState.Player.YawPitch = playerPositionPacket.YawPitch;
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
            case EntityPositionSyncPacket entityPositionSyncPacket:
            {
                SetPosition(entityPositionSyncPacket.EntityId, entityPositionSyncPacket.Position, false);
                break;
            }
            case MoveEntityPositionRotationPacket moveEntityPositionRotationPacket:
            {
                SetPosition(moveEntityPositionRotationPacket.EntityId, moveEntityPositionRotationPacket.Delta);
                break;
            }
            case MoveEntityPositionPacket moveEntityPositionPacket:
            {
                SetPosition(moveEntityPositionPacket.EntityId, moveEntityPositionPacket.Delta);
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
                var (chunkX, chunkZ) = (levelChunkWithLightPacket.ChunkX, levelChunkWithLightPacket.ChunkZ);
                clientState.Level.Chunks.AddOrUpdate((chunkX, chunkZ), levelChunkWithLightPacket.Chunk, (location, _) =>
                {
                    var oldChunk = clientState.Level.Chunks[location];
                    oldChunk = levelChunkWithLightPacket.Chunk;
                    return oldChunk;
                });
                break;
            }
            case ForgetLevelChunkPacket forgetLevelChunkPacket:
            {
                var (chunkX, chunkZ) = (forgetLevelChunkPacket.ChunkX, forgetLevelChunkPacket.ChunkZ);
                clientState.Level.Chunks.TryRemove((chunkX, chunkZ), out _);
                break;
            }
        }
    }

    private void SetPosition(int entityId, Vector3<double> newPosition, bool delta = true)
    {
        var (key, position) = _players.FirstOrDefault(x => x.Key.EntityId == entityId);

        if (key == null) return;

        if (delta)
        {
            position.X += newPosition.X;
            position.Y += newPosition.Y;
            position.Z += newPosition.Z;
        }
        else
        {
            position = newPosition;
        }

        _players[key] = position;
    }

    public MovePlayerPositionRotationPacket Move(double x, double y, double z)
    {
        var result = new MovePlayerPositionRotationPacket
        {
            X = x,
            Y = y,
            Z = z,
            Yaw = 0,
            Pitch = 0,
            Flags = MovePlayerPositionRotationPacket.MovementFlags.None
        };
        clientState.Player.Position.X = result.X;
        clientState.Player.Position.Y = result.Y;
        clientState.Player.Position.Z = result.Z;
        clientState.Player.YawPitch.X = result.Yaw;
        return result;
    }

    private static void InterpolateToCoordinates(IMinecraftClient client, Vector3<double> targetPosition)
    {
        const float baseSpeed = 0.25f;
        const float stoppingDistance = 0.25f;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var currentPosition = client.ClientState.Player.Position;
                var direction = new Vector3<double>(
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
                client.ClientState.Player.YawPitch.X = NormalizeYaw(targetYaw);
                var pitchDegrees = client.ClientState.Player.YawPitch.Y;

                var result = new MovePlayerPositionRotationPacket
                {
                    X = newPosition.X,
                    Y = newPosition.Y,
                    Z = newPosition.Z,
                    Yaw = (float)client.ClientState.Player.YawPitch.X,
                    Pitch = (float)pitchDegrees,
                    Flags = MovePlayerPositionRotationPacket.MovementFlags.None
                };

                client.ClientState.Player.Position.X = result.X;
                client.ClientState.Player.Position.Y = result.Y;
                client.ClientState.Player.Position.Z = result.Z;

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

    private static float CalculateYawToTarget(Vector3<double> currentPosition, Vector3<double> targetPosition)
    {
        var deltaX = targetPosition.X - currentPosition.X;
        var deltaZ = targetPosition.Z - currentPosition.Z;

        var yaw = (float)(Math.Atan2(-deltaX, deltaZ) * (180 / Math.PI));
        return NormalizeYaw(yaw);
    }
}
