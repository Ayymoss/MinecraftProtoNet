using System.Net.Sockets;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Handshaking.Serverbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Packets.Status.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core;

public class MinecraftClient(Connection connection, IPacketService packetService) : IMinecraftClient
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ProtocolState State { get; set; } = ProtocolState.Handshaking;
    public int ProtocolVersion { get; set; } = -1; // Unknown

    public async Task ConnectAsync(string host, int port)
    {
        AnsiConsole.MarkupLine($"[grey][[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{State.ToString()}[/]");

        await connection.ConnectAsync(host, port);

        _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

        const int intention = 2; // 1 Status - 2 Login - 3 Transfer
        var handshakePacket = new HandshakePacket
        {
            ProtocolVersion = 769, //ProtocolVersion, // Automate the protocol version from Status response.
            ServerAddress = host,
            ServerPort = (ushort)port,
            NextState = intention // This should be intention dependant by the caller
        };
        await SendPacketAsync(handshakePacket);
        State = intention switch
        {
            1 => ProtocolState.Status,
            2 => ProtocolState.Login,
            _ => ProtocolState.Transfer
        };

        AnsiConsole.MarkupLine($"[grey][[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{State.ToString()}[/]");

        switch (State)
        {
            case ProtocolState.Status:
                await SendPacketAsync(new StatusRequestPacket());
                break;
            case ProtocolState.Login:
                await SendPacketAsync(new LoginStartPacket
                    { Username = "JeffMaxxing", Uuid = new Guid("6f29c8b4-f0e7-40a3-a432-0ce0b97cebf0") });
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

    public async Task SendPacketAsync(IOutgoingPacket packet)
    {
        await connection.SendPacketAsync(packet, _cancellationTokenSource.Token);
    }

    private async Task ListenForPacketsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packetBuffer = await connection.ReadPacketBytesAsync(cancellationToken);
                var reader = new PacketBufferReader(packetBuffer);
                var packetId = reader.ReadVarInt();
                var packet = packetService.CreateIncomingPacket(State, packetId);
                packet.Deserialize(ref reader);
                await packetService.HandlePacketAsync(packet, this);
            }
        }
        catch (EndOfStreamException)
        {
            AnsiConsole.MarkupLine("\n[deepskyblue1]Connection closed by server.[/]");
        }
        catch (IOException ex) when (ex.InnerException is SocketException
                                     {
                                         SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted
                                     })
        {
            AnsiConsole.MarkupLine("[deepskyblue1]Connection forcibly closed by the remote host.[/]");
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Listening for packets cancelled.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("\n[red]Error while listening for packets:[/]");
            AnsiConsole.WriteException(ex);
        }
        finally
        {
            Environment.Exit(1);
        }
    }
}
