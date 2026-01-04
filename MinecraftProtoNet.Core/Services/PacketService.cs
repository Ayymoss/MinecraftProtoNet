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
                var existingHandler = _handlers[state][packetId];
                throw new ArgumentException($"Handler for state {state} and packet ID 0x{packetId:X2} is already registered to {existingHandler.GetType().Name}. Cannot add {handler.GetType().Name}.");
            }
        }
    }

    public async Task HandlePacketAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        var packetId = packet.GetPacketAttributeValue(p => p.PacketId);
        if (_handlers.TryGetValue(client.ProtocolState, out var stateHandlers) &&
            stateHandlers.TryGetValue(packetId, out var handler))
        {
            await handler.HandleAsync(packet, client);
        }
    }

    public IClientboundPacket CreateIncomingPacket(ProtocolState state, int packetId)
    {
        return PacketRegistry.CreateIncomingPacket(state, packetId);
    }
}
