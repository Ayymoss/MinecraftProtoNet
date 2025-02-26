using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Services;

public class PacketService : IPacketService
{
    private readonly Dictionary<ProtocolState, Dictionary<int, IPacketHandler>> _handlers = new();
    private readonly IEnumerable<IPacketHandler> _allHandlers;

    public PacketService(IEnumerable<IPacketHandler> allHandlers)
    {
        _allHandlers = allHandlers;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        foreach (var handler in _allHandlers)
        {
            RegisterHandler(handler);
        }
    }

    private void RegisterHandler(IPacketHandler handler)
    {
        foreach (var (state, packetId) in handler.RegisteredPackets)
        {
            if (!_handlers.TryGetValue(state, out var value))
            {
                value = new Dictionary<int, IPacketHandler>();
                _handlers[state] = value;
            }

            if (!value.TryAdd(packetId, handler))
            {
                throw new ArgumentException($"Handler for state {state} and packet ID {packetId} is already registered.");
            }
        }
    }

    public async Task HandlePacketAsync(Packet packet, IMinecraftClient client)
    {
        if (_handlers.TryGetValue(client.State, out var stateHandlers) && stateHandlers.TryGetValue(packet.PacketId, out var handler))
        {
            await handler.HandleAsync(packet, client);
        }
    }

    public Packet CreateIncomingPacket(ProtocolState state, int packetId)
    {
        Packet packet = state switch
        {
            ProtocolState.Handshaking => packetId switch
            {
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Handshaking state.")
            },
            ProtocolState.Status => packetId switch
            {
                0x00 => new Packets.Status.Clientbound.StatusResponsePacket(),
                0x01 => new Packets.Status.Clientbound.PongResponsePacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Status state.")
            },
            ProtocolState.Login => packetId switch
            {
                0x00 => new Packets.Login.Clientbound.DisconnectLoginPacket(),
                0x02 => new Packets.Login.Clientbound.LoginSuccessPacket(),
                0x03 => new Packets.Login.Clientbound.SetCompressionPacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Login state.")
            },
            ProtocolState.Configuration => packetId switch
            {
                0x01 => new Packets.Configuration.Clientbound.CustomPayloadPacket(),
                0x02 => new Packets.Configuration.Clientbound.DisconnectPacket(),
                0x03 => new Packets.Configuration.Clientbound.FinishConfigurationPacket(),
                0x04 => new Packets.Configuration.Clientbound.KeepAlivePacket(),
                0x07 => new Packets.Configuration.Clientbound.RegistryDataPacket(),
                0x0C => new Packets.Configuration.Clientbound.UpdateEnabledFeaturesPacket(),
                0x0D => new Packets.Configuration.Clientbound.UpdateTagsPacket(),
                0x0E => new Packets.Configuration.Clientbound.SelectKnownPacksPacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Configuration state.")
            },
            ProtocolState.Play => packetId switch
            {
                0x2C => new Packets.Play.Clientbound.LoginPacket(),
                0x0B => new Packets.Play.Clientbound.ChangeDifficultyPacket(),
                0x3A => new Packets.Play.Clientbound.PlayerAbilitiesPacket(),
                0x63 => new Packets.Play.Clientbound.SetHeldSlotPacket(),
                0x7E => new Packets.Play.Clientbound.UpdateRecipesPacket(),
                0x1F => new Packets.Play.Clientbound.EntityEventPacket(),
                0x42 => new Packets.Play.Clientbound.PlayerPositionPacket(),
                0x23 => new Packets.Play.Clientbound.GameEventPacket(),
                0x28 => new Packets.Play.Clientbound.LevelChunkWithLightPacket(),
                0x27 => new Packets.Play.Clientbound.KeepAlivePacket(),
                0x62 => new Packets.Play.Clientbound.SetHealthPacket(),
                0x3E => new Packets.Play.Clientbound.PlayerCombatKillPacket(),
                0x30 => new Packets.Play.Clientbound.MoveEntityPositionRotationPacket(),
                0x6B => new Packets.Play.Clientbound.SetTimePacket(),
                0x5B => new Packets.Play.Clientbound.SetDefaultSpawnPositionPacket(),
                0x78 => new Packets.Play.Clientbound.TickingStatePacket(),
                0x79 => new Packets.Play.Clientbound.TickingStepPacket(),
                0x2F => new Packets.Play.Clientbound.MoveEntityPositionPacket(),
                0x1D => new Packets.Play.Clientbound.DisconnectPacket(),
                _ => new UnknownPacket() // TODO: Remove when packets implemented.
                //_ => throw new ArgumentOutOfRangeException(nameof(packetId),
                //    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Play state.")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), $"Invalid protocol state {state}.")
        };

        if (packet is UnknownPacket)
        {
            AnsiConsole.MarkupLine($"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [blue][[->CLIENT]][/] " +
                                   $"[red]Unknown packet for state {state} and ID {packetId} (0x{packetId:X2})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [blue][[->CLIENT]][/] {packet.GetType().FullName?.NamespaceToPrettyString()} " +
                $"[white]Creating packet for state {state} and ID {packetId} (0x{packetId:X2})[/]");
        }

        return packet;
    }
}
