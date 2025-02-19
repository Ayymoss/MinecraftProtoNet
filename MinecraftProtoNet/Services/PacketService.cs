using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
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
        else
        {
            // Log or handle unhandled packets.
            Console.WriteLine($"Unhandled packet: {packet.PacketId} in state {client.State}");
        }
    }

    public Packet CreateIncomingPacket(ProtocolState state, int packetId)
    {
        Packet packet = state switch
        {
            ProtocolState.Handshaking => packetId switch
            {
                _ => throw new ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID {packetId} (0x{packetId:X2}) for Handshaking state.")
            },
            ProtocolState.Status => packetId switch
            {
                0x00 => new Packets.Status.Clientbound.StatusResponsePacket(),
                0x01 => new Packets.Status.Clientbound.PongResponsePacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID {packetId} (0x{packetId:X2}) for Status state.")
            },
            ProtocolState.Login => packetId switch
            {
                0x00 => new Packets.Login.Clientbound.DisconnectLoginPacket(),
                0x02 => new Packets.Login.Clientbound.LoginSuccessPacket(),
                0x03 => new Packets.Login.Clientbound.SetCompressionPacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID {packetId} (0x{packetId:X2}) for Login state.")
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
                _ => throw new ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID {packetId} (0x{packetId:X2}) for Configuration state.")
            },
            ProtocolState.Play => packetId switch
            {
                _ => throw new ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID {packetId} (0x{packetId:X2}) for Play state.")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), $"Invalid protocol state {state}.")
        };

        AnsiConsole.MarkupLine($"[grey][[DEBUG]][/] [blue][[->CLIENT]][/] [cyan][[{packet.GetType().Name}]][/] [white]Creating packet for state {state} and ID {packetId} (0x{packetId:X2})[/]");
        return packet;
    }
}
