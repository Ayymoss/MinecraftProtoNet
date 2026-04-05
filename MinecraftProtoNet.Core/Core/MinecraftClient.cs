using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Actions;
using MinecraftProtoNet.Core.Auth;
using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Handshaking.Serverbound;
using MinecraftProtoNet.Core.Packets.Login.Serverbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Packets.Status.Serverbound;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;
using MinecraftProtoNet.Core.Utilities;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Configuration;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Core.Core;

public class MinecraftClient : IMinecraftClient
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPacketSender _connection;
    private readonly IPacketService _packetService;
    private readonly IPacketProcessor _packetProcessor;
    private readonly IPhysicsService _physicsService;
    private readonly IGameLoop _gameLoop;
    private readonly IHumanizer _humanizer;
    private readonly HumanizerConfig _humanizerConfig;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly CommandRegistry _commandRegistry;
    private readonly ILogger<MinecraftClient> _logger;
    private readonly Thread? _mainThread;

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
        IPacketSender connection,
        IPacketService packetService,
        IPacketProcessor packetProcessor,
        IPhysicsService physicsService,
        IGameLoop gameLoop,
        IHumanizer humanizer,
        IOptions<HumanizerConfig> humanizerConfig,
        CommandRegistry commandRegistry,
        ILogger<MinecraftClient> logger)
    {
        _serviceProvider = serviceProvider;
        State = state;
        _connection = connection;
        _packetService = packetService;
        _packetProcessor = packetProcessor;
        _physicsService = physicsService;
        _gameLoop = gameLoop;
        _humanizer = humanizer;
        _humanizerConfig = humanizerConfig.Value;
        _commandRegistry = commandRegistry;
        _logger = logger;
        
        // Store the current thread as the main thread.
        // In a typical client implementation, this would be the thread that creates the client.
        // For Baritone compatibility, we check if code is running on this thread.
        _mainThread = Thread.CurrentThread;
        
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
        if (_connection is Connection conn)
        {
            conn.EnableEncryption(sharedSecret);
        }
    }

    public void EnableCompression(int threshold)
    {
        if (_connection is Connection conn)
        {
            conn.EnableCompression(threshold);
        }
    }

    public async Task ConnectAsync(string host, int port, bool isSnapshot = false)
    {
        // Always clean up any previous connection state before connecting
        // This handles the case where the server disconnected us (e.g., unverified_username)
        // and the user clicks Connect again without explicitly disconnecting first
        if (IsConnected || ProtocolState != ProtocolState.Handshaking)
        {
            _logger.LogInformation("Cleaning up previous connection state before reconnecting");
            await DisconnectAsync();
        }

        _logger.LogDebug("Switching protocol state: {ProtocolState}", ProtocolState);

        // Create a fresh CancellationTokenSource for this connection session
        _cancellationTokenSource = new CancellationTokenSource();

        // Reset packet processor for the new connection session
        _packetProcessor.Reset();

        if (_connection is Connection conn)
        {
            await conn.ConnectAsync(host, port);
        }
        IsConnected = true;
        State.ConnectedServerHost = host;

        _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

        const int intention = ProtocolConstants.Intention.Login;

        var protocolVersion = isSnapshot 
            ? ProtocolConstants.GetSnapshotProtocolVersion() 
            : ProtocolConstants.ProtocolVersion;

        var handshakePacket = new ClientIntentionPacket
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

    /// <summary>
    /// Cleans up connection state after the server disconnects us unexpectedly.
    /// Resets the connection so ConnectAsync can work again without requiring
    /// an explicit DisconnectAsync call from the UI.
    /// </summary>
    private async Task CleanupAfterServerDisconnect()
    {
        IsConnected = false;
        _packetProcessor.Close();
        await _gameLoop.StopAsync();

        // Reset connection so it can be reused
        if (_connection is Connection conn)
        {
            conn.Disconnect();
        }

        ProtocolState = ProtocolState.Handshaking;
    }

    public async Task DisconnectAsync()
    {
        IsConnected = false;

        // Close packet processor to stop enqueuing
        _packetProcessor.Close();

        // Stop the game loop FIRST so it stops sending packets
        await _gameLoop.StopAsync();

        // Cancel packet listener
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        // Disconnect (not dispose) the connection so it can be reused for reconnection
        if (_connection is Connection conn)
        {
            conn.Disconnect();
        }

        // Reset protocol state for next connection
        State.ConnectedServerHost = null;
        ProtocolState = ProtocolState.Handshaking;
    }

    /// <inheritdoc />
    public async Task SendChatMessageAsync(string message, CancellationToken ct = default)
    {
        // Resolve sinks from the service provider
        IChatSink sink = State.BotSettings.RedirectChat
            ? _serviceProvider.GetRequiredService<WebcoreChatSink>()
            : _serviceProvider.GetRequiredService<DefaultChatSink>();

        await sink.EmitAsync(message, ct);
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
                byte[] packetBuffer;
                if (_connection is Connection conn)
                {
                    packetBuffer = await conn.ReadPacketBytesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogError("ReadPacketBytesAsync is only supported with Connection implementation");
                    break;
                }
                var reader = new PacketBufferReader(packetBuffer);
                var packetId = reader.ReadVarInt();


                var packet = _packetService.CreateIncomingPacket(ProtocolState, packetId);

                // Layer 1: Packet-level resilience — each packet is length-prefixed so
                // the stream position is always correct even if Deserialize fails.
                // A bad component reader in one packet must not kill the connection.
                try
                {
                    packet.Deserialize(ref reader);
                }
                catch (Exception ex) when (packet is not UnknownPacket)
                {
                    _logger.LogWarning(ex, "[->CLIENT] Failed to deserialize {PacketType} (0x{PacketId:X2}) — skipping packet",
                        packet.GetType().Name, packetId);
                    continue;
                }

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

                // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/PacketUtils.java
                // Vanilla queues Play-state packets for the game thread via PacketProcessor.
                // Before the GameLoop starts (IsActive=false), handle immediately (safe: single thread).
                if (ProtocolState == ProtocolState.Play && _packetProcessor.IsActive)
                {
                    var handler = _packetService.GetHandler(ProtocolState, packetId);
                    if (handler != null)
                    {
                        _packetProcessor.Enqueue(packet, handler, this);
                    }
                }
                else
                {
                    await _packetService.HandlePacketAsync(packet, this);
                }
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogError(ex, "Connection closed by server");
                await CleanupAfterServerDisconnect();
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
                await CleanupAfterServerDisconnect();
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
                _logger.LogError(ex, "Error while listening for packets (state={State})", ProtocolState);
            }
        }
    }

    public async Task HandleChatMessageAsync(Guid senderGuid, string bodyMessage)
    {
        if (!bodyMessage.StartsWith("!")) return;

        // Block external ! commands on remote servers (only bot + authorized players allowed)
        if (_humanizerConfig.BlockExternalCommandsOnRemote && _humanizer.IsRemoteServer)
        {
            var ourUuid = AuthResult?.Uuid;
            var isSelf = ourUuid.HasValue && senderGuid == ourUuid.Value;
            var isAuthorized = _humanizerConfig.AuthorizedPlayerUuids
                .Any(id => Guid.TryParse(id, out var parsed) && parsed == senderGuid);

            if (!isSelf && !isAuthorized)
            {
                _logger.LogDebug("Blocked external command from {Sender}: {Message}", senderGuid, bodyMessage);
                return;
            }
        }

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
        if (!State.LocalPlayer.HasEntity)
        {
            return;
        }
        
        try
        {
            await _physicsService.PhysicsTickAsync(
                State.LocalPlayer.Entity, 
                State.Level, 
                this,
                prePhysicsCallback);

            // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:284-285
            // Java calls ensureHasSentCarriedItem() every tick in MultiPlayerGameMode.tick()
            // This ensures the server always knows which hotbar slot is selected, even when
            // Baritone changes it without going through the InteractionManager.
            await InteractionManager.EnsureHasSentCarriedItemAsync();
            
            // Handle input clicks (Baritone integration)
            var entity = State.LocalPlayer.Entity;
            if (entity.Input.ClickRight)
            {
                _logger.LogInformation("PhysicsTick: Right click input detected, calling InteractAsync");
                await InteractionManager.InteractAsync();
            }
            if (entity.Input.ClickLeft)
            {
                // _logger.LogInformation("PhysicsTick: Left click input detected, calling DigBlockAsync");
                await InteractionManager.DigBlockAsync();
            }
            else
            {
                await InteractionManager.ResetBlockRemovingAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PhysicsTickAsync: Exception in physics tick");
            throw;
        }
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

    /// <summary>
    /// Checks if the current thread is the main/game thread.
    /// Equivalent to Java's Minecraft.isSameThread().
    /// Used by Baritone to validate thread safety for certain operations.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:72-74
    /// </summary>
    public bool IsSameThread()
    {
        // Once the GameLoop is running, the game thread is the authoritative "main" thread.
        // Before that, fall back to the constructor thread.
        if (_packetProcessor.IsActive)
        {
            return _packetProcessor.IsSameThread();
        }

        return _mainThread != null && Thread.CurrentThread == _mainThread;
    }
}
