using System.Net.Sockets;
using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Auth;
using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Packets.Handshaking.Serverbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Packets.Status.Serverbound;
using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core;

public class MinecraftClient : IMinecraftClient
{
    private readonly Connection _connection;
    private readonly IPacketService _packetService;
    private readonly IPhysicsService _physicsService = new PhysicsService();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly CommandRegistry _commandRegistry = new();

    public IPathFollowerService PathFollowerService { get; } = new PathFollowerService();


    public ClientState State { get; } = new();
    public AuthResult AuthResult { get; set; }
    public ProtocolState ProtocolState { get; set; } = ProtocolState.Handshaking;
    public int ProtocolVersion { get; set; } = -1; // Unknown

    public MinecraftClient(Connection connection, IPacketService packetService)
    {
        _connection = connection;
        _packetService = packetService;
        _commandRegistry.AutoRegisterCommands();
    }

    /// <summary>
    /// Creates an action context for invoking actions from external code (API, console, etc.)
    /// </summary>
    public IActionContext CreateActionContext() => new ActionContext(this, State, AuthResult);

    public async Task<bool> AuthenticateAsync()
    {
        var authResult = await AuthenticationFlow.AuthenticateAsync();
        if (authResult is null) return false;
        AuthResult = authResult;
        return true;
    }

    public void EnableEncryption(byte[] sharedSecret)
    {
        _connection.EnableEncryption(sharedSecret);
    }

    public void EnableCompression(int threshold)
    {
        _connection.EnableCompression(threshold);
    }

    public async Task ConnectAsync(string host, int port, bool isSnapshot = false)
    {
        AnsiConsole.MarkupLine(
            $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{ProtocolState.ToString()}[/]");

        await _connection.ConnectAsync(host, port);

        _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

        const int intention = 2; // 1 Status - 2 Login - 3 Transfer

        // 775 is the protocol version for 1.21.x
        // Snapshot uses bit 30 set | 287 (for 26.1 Snapshot 1)
        var protocolVersion = isSnapshot ? (1 << 30) | 287 : 775;

        var handshakePacket = new HandshakePacket
        {
            ProtocolVersion = protocolVersion,
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
            case ProtocolState.Login:
                await SendPacketAsync(new HelloPacket { Username = AuthResult.Username, Uuid = AuthResult.Uuid });
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task DisconnectAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _connection.Dispose();
    }

    public async Task SendPacketAsync(IServerboundPacket packet)
    {
        await _connection.SendPacketAsync(packet, _cancellationTokenSource.Token);
    }

    private async Task ListenForPacketsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var packetBuffer = await _connection.ReadPacketBytesAsync(cancellationToken);
                var reader = new PacketBufferReader(packetBuffer);
                var packetId = reader.ReadVarInt();
                var packet = _packetService.CreateIncomingPacket(ProtocolState, packetId);
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

                await _packetService.HandlePacketAsync(packet, this);
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
        if (!bodyMessage.StartsWith("!")) return;

        var parts = bodyMessage[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var commandName = parts[0];
        var args = parts.Skip(1).ToArray();

        var context = new CommandContext(this, State, AuthResult, senderGuid, args);
        await _commandRegistry.ExecuteAsync(commandName, context);
    }


    // The old PhysicsTickAsync was in MinecraftClient.Physics.cs.
    // It is now replaced by this implementation.
    public async Task PhysicsTickAsync()
    {
        if (!State.LocalPlayer.HasEntity) return;
        // The PathFollowerService's UpdatePathFollowingInput will be called by the PhysicsService via a delegate.
        // This ensures that input decisions (like wanting to jump or sprint based on path) are made *before* physics calculations for the tick.
        await _physicsService.PhysicsTickAsync(State.LocalPlayer.Entity, State.Level, SendPacketAsync,
            (entity) => PathFollowerService.UpdatePathFollowingInput(entity)
        );
    }


    public async Task SendChatSessionUpdate()
    {
        if (AuthResult.ChatSession is null)
        {
            Console.WriteLine("[WARN] Skipping ChatSessionUpdate: AuthResult.ChatSession is null.");
            // Log.Warning("Skipping ChatSessionUpdate: AuthResult.ChatSession is null.");
            return;
        }

        Console.WriteLine($"[DEBUG] Sending ChatSessionUpdatePacket. SessionId: {AuthResult.ChatSession.ChatContext.ChatSessionGuid}");

        await SendPacketAsync(new ChatSessionUpdatePacket
        {
            SessionId = AuthResult.ChatSession.ChatContext.ChatSessionGuid,
            ExpiresAt = AuthResult.ChatSession.ExpiresAtEpochMs,
            PublicKey = AuthResult.ChatSession.PublicKeyDer,
            KeySignature = AuthResult.ChatSession.MojangSignature
        });

        Console.WriteLine("[green]Sent ChatSessionUpdatePacket.[/]");
    }
}
