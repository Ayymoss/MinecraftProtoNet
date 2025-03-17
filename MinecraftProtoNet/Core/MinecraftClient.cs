using System.Net.Sockets;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Handshaking.Serverbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Packets.Status.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core;

public class MinecraftClient(Connection connection, IPacketService packetService) : IMinecraftClient
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

    // TODO: Find a better place for game specific logic.
    public void PhysicsTick()
    {
    }

    public async Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage)
    {
        var sender = State.Level.GetPlayerByUuid(senderGuid);

        if (bodyMessage.StartsWith("!say"))
        {
            var message = bodyMessage.Split(" ").Skip(1).ToArray();
            await SendPacketAsync(new ChatPacket(string.Join(" ", message)));
        }

        if (bodyMessage.StartsWith("!getblock"))
        {
            var coords = bodyMessage.Split(" ");
            if (coords.Length == 4)
            {
                var x = int.Parse(coords[1]);
                var y = int.Parse(coords[2]);
                var z = int.Parse(coords[3]);
                var block = State.Level.GetBlockAt(x, y, z);
                var message = block != null
                    ? $"Block: ({block.Id}) {block.Name}"
                    : $"Block not found at {x}, {y}, {z}";
                await SendPacketAsync(new ChatPacket(message));
            }
        }

        if (bodyMessage.StartsWith("!goto"))
        {
            var coords = bodyMessage.Split(" ");
            if (coords.Length is 4 or 5)
            {
                var x = float.Parse(coords[1]);
                var y = float.Parse(coords[2]);
                var z = float.Parse(coords[3]);
                var speed = 0.25f;
                if (coords.Length is 5) float.TryParse(coords[4], out speed);
                ClientManagerHelpers.InterpolateToCoordinates(this, new Vector3<double>(x, y, z), speed);
                await SendPacketAsync(new ChatPacket($"Moving to {x:N2}, {y:N2}, {z:N2}"));
            }
        }

        if (bodyMessage == "!ping")
        {
            await SendPacketAsync(new Packets.Play.Serverbound.PingRequestPacket
                { Payload = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds() });
        }

        if (sender is null) return;

        if (bodyMessage == "!pos")
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

        if (bodyMessage == "!here")
        {
            if (!sender.HasEntity)
            {
                await SendPacketAsync(new ChatPacket("Your position is not available."));
                return;
            }

            ClientManagerHelpers.InterpolateToCoordinates(this, sender.Entity.Position);
            await SendPacketAsync(
                new ChatPacket(
                    $"Moving to last location: {sender.Entity.Position.X:N2}, {sender.Entity.Position.Y:N2}, {sender.Entity.Position.Z:N2}"));
        }
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

        if (!State.LocalPlayer.HasEntity) throw new InvalidOperationException("Local player entity not found.");
        State.LocalPlayer.Entity.Position.X = result.X;
        State.LocalPlayer.Entity.Position.Y = result.Y;
        State.LocalPlayer.Entity.Position.Z = result.Z;
        State.LocalPlayer.Entity.YawPitch.X = result.Yaw;
        return result;
    }
}
