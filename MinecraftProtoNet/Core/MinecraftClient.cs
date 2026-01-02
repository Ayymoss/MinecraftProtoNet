using System.Net.Sockets;
using Microsoft.Extensions.Logging;
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

namespace MinecraftProtoNet.Core;

public class MinecraftClient : IMinecraftClient
{
    private readonly Connection _connection;
    private readonly IPacketService _packetService;
    private readonly IPhysicsService _physicsService;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly CommandRegistry _commandRegistry;
    private readonly ILogger<MinecraftClient> _logger;

    /// <summary>
    /// Raised when the client disconnects from the server.
    /// </summary>
    public event EventHandler<DisconnectReason>? OnDisconnected;

    public ClientState State { get; }
    public AuthResult? AuthResult { get; set; }
    public ProtocolState ProtocolState { get; set; } = ProtocolState.Handshaking;
    public int ProtocolVersion { get; set; } = -1; // Unknown
    public bool IsConnected { get; private set; }


    public MinecraftClient(
        IServiceProvider serviceProvider,
        ClientState state,
        Connection connection, 
        IPacketService packetService,
        IPhysicsService physicsService,
        CommandRegistry commandRegistry,
        ILogger<MinecraftClient> logger)
    {
        State = state;
        _connection = connection;
        _packetService = packetService;
        _physicsService = physicsService;
        _commandRegistry = commandRegistry;
        _commandRegistry = commandRegistry;
        _logger = logger;
        
        // Manual instantiation for now if not provided by DI, or could be added to constructor params.
        // For this refactor, simplest is to instantiate it here using 'this'.
        InteractionManager = new InteractionManager(this, LoggingConfiguration.CreateLogger<InteractionManager>());
        
        _commandRegistry.AutoRegisterCommands(serviceProvider);
    }

    public IInteractionManager InteractionManager { get; }

    /// <summary>
    /// Creates an action context for invoking actions from external code (API, console, etc.)
    /// </summary>
    public IActionContext CreateActionContext() => new ActionContext(this, State, AuthResult!);

    public async Task<bool> AuthenticateAsync()
    {
        var authResult = await AuthenticationFlow.AuthenticateAsync();
        if (authResult is null) return false;
        AuthResult = authResult;
        
        // Sync local player info
        State.LocalPlayer.Uuid = authResult.Uuid;
        State.LocalPlayer.Username = authResult.Username;
        
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
        _logger.LogDebug("Switching protocol state: {ProtocolState}", ProtocolState);

        await _connection.ConnectAsync(host, port);
        IsConnected = true;

        _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

        const int intention = ProtocolConstants.Intention.Login;

        var protocolVersion = isSnapshot 
            ? ProtocolConstants.GetSnapshotProtocolVersion() 
            : ProtocolConstants.ProtocolVersion;

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
            ProtocolConstants.Intention.Status => ProtocolState.Status,
            ProtocolConstants.Intention.Login => ProtocolState.Login,
            _ => ProtocolState.Transfer
        };


        _logger.LogDebug("Switching protocol state: {ProtocolState}", ProtocolState);

        switch (ProtocolState)
        {
            case ProtocolState.Status:
                await SendPacketAsync(new StatusRequestPacket());
                break;
            case ProtocolState.Login:
                await SendPacketAsync(new HelloPacket { Username = AuthResult!.Username, Uuid = AuthResult.Uuid });
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task DisconnectAsync()
    {
        IsConnected = false;
        await _cancellationTokenSource.CancelAsync();
        _connection.Dispose();
    }

    public async Task SendPacketAsync(IServerboundPacket packet, CancellationToken cancellationToken = default)
    {
        await _connection.SendPacketAsync(packet, cancellationToken == default ? _cancellationTokenSource.Token : cancellationToken);
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
                    _logger.LogWarning("[->CLIENT] Unknown packet for state {ProtocolState} and ID {PacketId} (0x{PacketIdHex:X2})",
                        ProtocolState, packetId, packetId);
                }
                else if (!packet.GetPacketAttributeValue(p => p.Silent))
                {
                    _logger.LogDebug("[->CLIENT] {PacketType} {Properties}",
                        packet.GetType().FullName?.NamespaceToPrettyString(packetId),
                        packet.GetPropertiesAsString());
                }

                await _packetService.HandlePacketAsync(packet, this);
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogError(ex, "Connection closed by server");
                IsConnected = false;
                OnDisconnected?.Invoke(this, DisconnectReason.EndOfStream);
                break;
            }
            catch (IOException ex) when (ex.InnerException is SocketException
                                         {
                                             SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted
                                         } socket)
            {
                _logger.LogError(ex, "Connection forcibly closed by the remote host. ErrorCode: {ErrorCode}, SocketErrorCode: {SocketErrorCode}",
                    socket.ErrorCode, socket.SocketErrorCode);
                IsConnected = false;
                OnDisconnected?.Invoke(this, DisconnectReason.ConnectionReset);
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Listening for packets cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while listening for packets");
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

        var context = new CommandContext(this, State, AuthResult!, senderGuid, args);
        await _commandRegistry.ExecuteAsync(commandName, context);
    }


    // The old PhysicsTickAsync was in MinecraftClient.Physics.cs.
    // It is now replaced by this implementation.
    public async Task PhysicsTickAsync(Action<State.Entity>? prePhysicsCallback = null)
    {
        if (!State.LocalPlayer.HasEntity) return;
        
        await _physicsService.PhysicsTickAsync(
            State.LocalPlayer.Entity, 
            State.Level, 
            this,
            prePhysicsCallback);
    }


    public async Task SendChatSessionUpdate()
    {
        if (AuthResult?.ChatSession is null)
        {
            _logger.LogWarning("Skipping ChatSessionUpdate: AuthResult.ChatSession is null");
            return;
        }

        _logger.LogDebug("Sending ChatSessionUpdatePacket. SessionId: {SessionId}",
            AuthResult!.ChatSession.ChatContext.ChatSessionGuid);

        await SendPacketAsync(new ChatSessionUpdatePacket
        {
            SessionId = AuthResult!.ChatSession.ChatContext.ChatSessionGuid,
            ExpiresAt = AuthResult.ChatSession.ExpiresAtEpochMs,
            PublicKey = AuthResult.ChatSession.PublicKeyDer,
            KeySignature = AuthResult.ChatSession.MojangSignature
        });

        _logger.LogDebug("Sent ChatSessionUpdatePacket");
    }
}
