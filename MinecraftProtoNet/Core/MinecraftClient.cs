using System.Net.Sockets;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Handlers.Meta;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Handshaking.Serverbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Packets.Status.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core;

public partial class MinecraftClient(Connection connection, IPacketService packetService) : IMinecraftClient
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    public ClientState State { get; } = new();

    public ProtocolState ProtocolState { get; set; } = ProtocolState.Handshaking;
    public int ProtocolVersion { get; set; } = -1; // Unknown

    public async Task ConnectAsync(string host, int port)
    {
        AnsiConsole.MarkupLine(
            $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{ProtocolState.ToString()}[/]");

        await connection.ConnectAsync(host, port);

        _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

        const int intention = 2; // 1 Status - 2 Login - 3 Transfer
        var handshakePacket = new HandshakePacket
        {
            ProtocolVersion = 769, //ProtocolVersion, // Automate the protocol version from Status response.
            ServerAddress = host,
            ServerPort = (ushort)port,
            NextState = intention // TODO: This should be intention dependant by the caller
        };
        await SendPacketAsync(handshakePacket);
        ProtocolState = intention switch
        {
            1 => ProtocolState.Status,
            2 => ProtocolState.Login,
            _ => ProtocolState.Transfer
        };

        AnsiConsole.MarkupLine(
            $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{ProtocolState.ToString()}[/]");

        switch (ProtocolState)
        {
            case ProtocolState.Status:
                await SendPacketAsync(new StatusRequestPacket());
                break;
            case ProtocolState.Login: // TODO: MSAL requires abstracting
                await SendPacketAsync(new LoginStartPacket
                    { Username = "MyNameDave", Uuid = new Guid("6f29c8b4-f0e7-40a3-a432-2ce0b97cebf0") });
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task DisconnectAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        connection.Dispose();
    }

    public async Task SendPacketAsync(IServerPacket packet)
    {
        await connection.SendPacketAsync(packet, _cancellationTokenSource.Token);
    }

    private async Task ListenForPacketsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var packetBuffer = await connection.ReadPacketBytesAsync(cancellationToken);
                var reader = new PacketBufferReader(packetBuffer);
                var packetId = reader.ReadVarInt();
                var packet = packetService.CreateIncomingPacket(ProtocolState, packetId);
                packet.Deserialize(ref reader);

                if (packet is UnknownPacket)
                {
                    AnsiConsole.MarkupLine($"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [blue][[->CLIENT]][/] " +
                                           $"[red]Unknown packet for state {ProtocolState} and ID {packetId} (0x{packetId:X2})[/]");
                }
                else if (!packet.GetPacketAttributeValue(p => p.Silent))
                {
                    AnsiConsole.Markup(
                        $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [blue][[->CLIENT]][/] " +
                        $"{packet.GetType().FullName?.NamespaceToPrettyString(packetId)} ");
                    AnsiConsole.WriteLine(packet.GetPropertiesAsString()); // Some strings include brackets.
                }

                await packetService.HandlePacketAsync(packet, this);
            }
            catch (EndOfStreamException ex)
            {
                AnsiConsole.MarkupLine(
                    $"\n[grey]{TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [deepskyblue1]Connection closed by server.[/]");
                AnsiConsole.WriteException(ex);
                Environment.Exit(1);
            }
            catch (IOException ex) when (ex.InnerException is SocketException
                                         {
                                             SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted
                                         } socket)
            {
                AnsiConsole.MarkupLine(
                    $"\n[grey]{TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [deepskyblue1]Connection forcibly closed by the remote host. EC: {socket.ErrorCode} - SEC: {socket.SocketErrorCode} - MSG: {socket.Message}[/]");
                AnsiConsole.WriteException(ex);
                Environment.Exit(1);
            }
            catch (OperationCanceledException ex)
            {
                AnsiConsole.MarkupLine(
                    $"\n[grey]{TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [red]Listening for packets cancelled.[/]");
                AnsiConsole.WriteException(ex);
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"\n[grey]{TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [red]Error while listening for packets:[/]");
                AnsiConsole.WriteException(ex);
            }
        }
    }

    public async Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage)
    {
        if (!State.LocalPlayer.HasEntity) return;
        var sender = State.Level.GetPlayerByUuid(senderGuid);
        var command = bodyMessage.Split(" ")[0];

        if (command == "!say")
        {
            var message = bodyMessage.Split(" ").Skip(1).ToArray();
            await SendPacketAsync(new ChatPacket(string.Join(" ", message)));
        }

        if (command == "!getblock")
        {
            var coords = bodyMessage.Split(" ").Skip(1).ToArray();
            if (coords.Length is 3)
            {
                var x = int.Parse(coords[0]);
                var y = int.Parse(coords[1]);
                var z = int.Parse(coords[2]);
                var block = State.Level.GetBlockAt(x, y, z);
                var message = block != null
                    ? $"Block: ({block.Id}) {block.Name}"
                    : $"Block not found at {x}, {y}, {z}";
                await SendPacketAsync(new ChatPacket(message));
            }
        }

        if (command == "!goto")
        {
            var coords = bodyMessage.Split(" ").Skip(1).ToArray();
            if (coords.Length is 3 or 4)
            {
                var x = float.Parse(coords[0]);
                var y = float.Parse(coords[1]);
                var z = float.Parse(coords[2]);
                var speed = 0.25f;
                if (coords.Length is 5) float.TryParse(coords[4], out speed);
                ClientManagerHelpers.InterpolateToCoordinates(this, new Vector3<double>(x, y, z), speed);
                await SendPacketAsync(new ChatPacket($"Moving to {x:N2}, {y:N2}, {z:N2}"));
            }
        }

        if (command == "!gotowalk")
        {
            var coords = bodyMessage.Split(" ").Skip(1).ToArray();
            if (coords.Length is 3)
            {
                var x = float.Parse(coords[0]);
                var y = float.Parse(coords[1]);
                var z = float.Parse(coords[2]);
                //await State.LocalPlayer.Entity.MoveToPosition(new Vector3<double>(x, y, z), State.Level, SendPacketAsync);

                //if (!result)
                //{
                //    await SendPacketAsync(new ChatPacket("I can't reach that position."));
                //    return;
                //}

                await SendPacketAsync(new ChatPacket("Moving to that position."));
            }
        }

        if (command == "!ping")
        {
            await SendPacketAsync(new Packets.Play.Serverbound.PingRequestPacket
                { Payload = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds() });
        }

        if (command == "!tps")
        {
            await SendPacketAsync(new ChatPacket($"TPS: {State.Level.GetCurrentServerTps():N2} " +
                                                 $"| MSPT: {State.Level.TickInterval:N2}ms " +
                                                 $"| Rate: {State.Level.GetTickRateMultiplier():N2}x"));
        }

        if (command == "!swing")
        {
            var message = bodyMessage.Split(" ").Skip(1).ToArray();
            if (Enum.TryParse<Hand>(string.Join(" ", message), true, out var swing))
                await SendPacketAsync(new SwingPacket { Hand = swing });
        }

        if (command == "!cmd")
        {
            var message = bodyMessage.Split(" ").Skip(1).ToArray();
            await SendPacketAsync(new ChatCommandPacket(string.Join(" ", message)));
        }

        if (command == "!slot")
        {
            var entity = State.LocalPlayer.Entity;
            var message = bodyMessage.Split(" ").Skip(1).ToArray();
            if (message.Length is 1)
            {
                var slot = short.Parse(message[0]);
                if (slot is < 0 or > 8)
                {
                    await SendPacketAsync(new ChatPacket("Slot must be between 0 and 8."));
                    return;
                }

                await SendPacketAsync(new SetCarriedItemPacket { Slot = slot });
                entity.HeldSlot = slot;
            }
            else
            {
                await SendPacketAsync(new ChatPacket($"Slot Held: {entity.HeldSlot} (0-8)"));
            }
        }

        if (command == "!place")
        {
            var entity = State.LocalPlayer.Entity;
            if (entity.HeldItem.ItemId is null)
            {
                await SendPacketAsync(new ChatPacket("You are not holding anything."));
                return;
            }

            await PlaceHelper();
        }

        if (command == "!lookat")
        {
            var args = bodyMessage.Split(" ").Skip(1).ToArray();
            if (args.Length is 3 or 4)
            {
                var x = float.Parse(args[0]);
                var y = float.Parse(args[1]);
                var z = float.Parse(args[2]);

                var face = BlockFace.Top;
                if (args.Length >= 4 && Enum.TryParse<BlockFace>(args[3], true, out var parsedFace))
                {
                    face = parsedFace;
                }

                await LookAtHelper(x, y, z, face);
            }
        }

        if (command == "!jump")
        {
            var entity = State.LocalPlayer.Entity;
            if (entity.IsOnGround)
            {
                entity.IsJumping = true;
                await SendPacketAsync(new ChatPacket("Jumping!"));
            }
            else
            {
                await SendPacketAsync(new ChatPacket("You are not on the ground."));
            }
        }

        if (command == "!placeit")
        {
            var args = bodyMessage.Split(" ").Skip(1).ToArray();
            if (args.Length is 3)
            {
                var x = float.Parse(args[0]);
                var y = float.Parse(args[1]);
                var z = float.Parse(args[2]);

                var heldBlock = State.LocalPlayer.Entity.HeldItem;
                if (heldBlock.ItemId is null) return;
                await LookAtHelper(x, y, z, BlockFace.Top);
                await PlaceHelper();
                await LookAtHelper(x - 1, y, z, BlockFace.Top);
                await PlaceHelper();
                await LookAtHelper(x + 1, y, z, BlockFace.Top);
                await PlaceHelper();
                await Task.Delay(100);
                await LookAtHelper(x, y + 1, z, BlockFace.Top);
                await PlaceHelper();
                State.LocalPlayer.Entity.IsJumping = true;
                await Task.Delay(100);
                await LookAtHelper(x, y + 2, z, BlockFace.Top);
                await PlaceHelper();
                await Task.Delay(500);
                await LookAtHelper(x, y + 1, z, BlockFace.South);
                var lookingAt = State.LocalPlayer.Entity.GetLookingAtBlock(State.Level);
                if (lookingAt is null) return;
                await SendPacketAsync(new PlayerActionPacket
                {
                    Status = PlayerActionPacket.StatusType.StartedDigging,
                    Position = lookingAt.BlockPosition.ToVector3<int, double>(),
                    Face = lookingAt.Face,
                    Sequence = State.LocalPlayer.Entity.IncrementSequence()
                });

                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(50);
                    await SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
                }

                await SendPacketAsync(new PlayerActionPacket
                {
                    Status = PlayerActionPacket.StatusType.FinishedDigging,
                    Position = lookingAt.BlockPosition.ToVector3<int, double>(),
                    Face = lookingAt.Face,
                    Sequence = State.LocalPlayer.Entity.IncrementSequence()
                });
            }
        }

        if (command == "!lookingat")
        {
            var entity = State.LocalPlayer.Entity;
            var hit = entity.GetLookingAtBlock(State.Level);
            if (hit is null)
            {
                await SendPacketAsync(new ChatPacket("I'm not looking at a block."));
                return;
            }

            var cursorPos = hit.GetInBlockPosition();
            var placementPos = hit.GetAdjacentBlockPosition();

            List<string> messages =
            [
                $"Name: {hit.Block?.Name} - Pos: {hit.BlockPosition}",
                $"Distance: {hit.Distance:N2}",
                $"Cursor: {cursorPos}",
                $"Face: {hit.Face}",
                $"Placement Pos: {placementPos}"
            ];

            foreach (var message in messages)
            {
                await SendPacketAsync(new ChatPacket(message));
                await Task.Delay(100);
            }
        }

        if (command == "!holding")
        {
            var heldItem = State.LocalPlayer.Entity.HeldItem;
            if (heldItem.ItemId is null)
            {
                await SendPacketAsync(new ChatPacket("You are not holding anything."));
                return;
            }

            var itemName = ClientState.ItemRegistry[heldItem.ItemId.Value];
            var message = $"Holding: {heldItem.ItemCount}x of {itemName} ({heldItem.ItemId})";
            await SendPacketAsync(new ChatPacket(message));
        }

        if (command == "!drop")
        {
            var entity = State.LocalPlayer.Entity;
            var heldItem = entity.HeldItem;
            if (heldItem.ItemId is null)
            {
                await SendPacketAsync(new ChatPacket("You are not holding anything."));
                return;
            }

            await SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.DropItemStack,
                Position = new Vector3<double>(0, 0, 0),
                Face = BlockFace.Bottom,
                Sequence = 0
            });
            entity.Inventory[(short)(entity.HeldSlot + 36)] = new Slot();
        }

        if (sender is null) return;

        if (command == "!pos")
        {
            if (!sender.HasEntity)
            {
                await SendPacketAsync(new ChatPacket("Your position is not available."));
                return;
            }

            var playerPos =
                $"{sender.Username} -> {sender.Entity.Position.X:N2}, {sender.Entity.Position.Y:N2}, {sender.Entity.Position.Z:N2}";
            var message = $"Last position: {playerPos}";
            await SendPacketAsync(new ChatPacket(message));
        }

        if (command == "!here")
        {
            if (!sender.HasEntity)
            {
                await SendPacketAsync(new ChatPacket("Your position is not available."));
                return;
            }

            var targetPosition = sender.Entity.Position;
            var pathFinder = new AStarPathFinder(State.Level);
            var result = pathFinder.FindPath(State.LocalPlayer.Entity.Position, targetPosition);
            if (result is null)
            {
                Console.WriteLine("Path not found.");
                return;
            }

            foreach (var vector in result)
            {
                Console.WriteLine(vector);
            }

            //await State.LocalPlayer.Entity.MoveToPosition(targetPosition, State.Level, SendPacketAsync);

            // TODO: Won't jump, has issues going up/down blocks. Seems to cause teleport packets, illegal movement?
            //if (!result)
            //{
            //    await SendPacketAsync(new ChatPacket("I can't reach your position."));
            //    return;
            //}

            await SendPacketAsync(new ChatPacket("Moving to your position."));
        }
    }

    private async Task PlaceHelper()
    {
        if (!State.LocalPlayer.HasEntity) return;

        var entity = State.LocalPlayer.Entity;
        var hit = entity.GetLookingAtBlock(State.Level);
        if (hit is null)
        {
            await SendPacketAsync(new ChatPacket("I'm not looking at a block."));
            return;
        }

        if (hit.Distance > 6)
        {
            await SendPacketAsync(new ChatPacket("Block is too far away."));
            return;
        }

        var cursor = hit.GetInBlockPosition();
        await SendPacketAsync(new UseItemOnPacket
        {
            Hand = Hand.MainHand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>(cursor.X, cursor.Y, cursor.Z),
            InsideBlock = false,
            Sequence = entity.IncrementSequence()
        });
        await SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
    }

    private async Task LookAtHelper(float x, float y, float z, BlockFace? face)
    {
        if (!State.LocalPlayer.HasEntity) return;
        var targetX = x + 0.5f;
        var targetY = y + 0.5f;
        var targetZ = z + 0.5f;

        if (face.HasValue)
        {
            switch (face)
            {
                case BlockFace.Bottom:
                    targetY = y;
                    break;
                case BlockFace.Top:
                    targetY = y + 1.0f;
                    break;
                case BlockFace.North:
                    targetZ = z;
                    break;
                case BlockFace.South:
                    targetZ = z + 1.0f;
                    break;
                case BlockFace.West:
                    targetX = x;
                    break;
                case BlockFace.East:
                    targetX = x + 1.0f;
                    break;
            }
        }

        var playerEyeX = State.LocalPlayer.Entity.Position.X;
        var playerEyeY = State.LocalPlayer.Entity.Position.Y + 1.62f;
        var playerEyeZ = State.LocalPlayer.Entity.Position.Z;

        var dx = targetX - playerEyeX;
        var dy = targetY - playerEyeY;
        var dz = targetZ - playerEyeZ;

        var yaw = (float)(-Math.Atan2(dx, dz) * (180 / Math.PI));
        var horizontalDistance = Math.Sqrt(dx * dx + dz * dz);
        var pitch = (float)(-Math.Atan2(dy, horizontalDistance) * (180 / Math.PI));

        await SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yaw,
            Pitch = pitch,
            Flags = MovementFlags.None
        });

        State.LocalPlayer.Entity.YawPitch = new Vector2<float>(yaw, pitch);
    }

    public void SetPosition(int entityId, Vector3<double> newPosition, bool delta = true)
    {
        var entity = State.Level.GetEntityOfId(entityId);
        if (entity is null) return;

        if (delta)
        {
            entity.Position.X += newPosition.X;
            entity.Position.Y += newPosition.Y;
            entity.Position.Z += newPosition.Z;
        }
        else
        {
            entity.Position = newPosition;
        }
    }

    public MovePlayerPositionRotationPacket Move(double x, double y, double z, float yaw = 0, float pitch = 0)
    {
        var result = new MovePlayerPositionRotationPacket
        {
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch,
            Flags = MovementFlags.None
        };

        if (!State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
        State.LocalPlayer.Entity.Position.X = result.X;
        State.LocalPlayer.Entity.Position.Y = result.Y;
        State.LocalPlayer.Entity.Position.Z = result.Z;
        State.LocalPlayer.Entity.YawPitch.X = result.Yaw;
        State.LocalPlayer.Entity.YawPitch.Y = result.Pitch;
        return result;
    }
}
