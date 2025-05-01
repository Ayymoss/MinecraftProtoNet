using System.Linq.Expressions;
using System.Reflection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using Spectre.Console;

namespace MinecraftProtoNet.Services;

public static class PacketRegistry
{
    private static readonly Dictionary<(ProtocolState State, int PacketId), Func<IClientboundPacket>> ClientboundPacketFactories;
    private static readonly Dictionary<Type, Attributes.PacketAttribute> PacketAttributes;
    private static readonly Dictionary<Type, List<(ProtocolState State, int PacketId)>> HandlerRegistrations;

    static PacketRegistry()
    {
        ClientboundPacketFactories = new Dictionary<(ProtocolState, int), Func<IClientboundPacket>>();
        PacketAttributes = new Dictionary<Type, Attributes.PacketAttribute>();
        HandlerRegistrations = new Dictionary<Type, List<(ProtocolState State, int PacketId)>>();

        var assembliesToScan = new[] { Assembly.GetExecutingAssembly() };

        var packetTypes = assembliesToScan
            .SelectMany(asm => asm.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToList();

        // 1. Populate Packet Attributes and Clientbound Factories
        foreach (var type in packetTypes.Where(t => typeof(IClientboundPacket).IsAssignableFrom(t)))
        {
            var attribute = type.GetCustomAttribute<Attributes.PacketAttribute>();
            if (attribute == null) continue;

            var key = (attribute.ProtocolState, attribute.PacketId);

            if (ClientboundPacketFactories.TryGetValue(key, out var packetFactory))
            {
                AnsiConsole.WriteLine(
                    $"Warning: Duplicate packet definition for State={key.ProtocolState}, ID=0x{key.PacketId:X2}. Existing: {packetFactory.Method.DeclaringType?.FullName}, New: {type.FullName}");
                continue;
            }

            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                AnsiConsole.WriteLine(
                    $"Warning: Packet type {type.FullName} does not have a parameterless constructor. Cannot create factory.");
                continue;
            }

            var factory = Expression.Lambda<Func<IClientboundPacket>>(Expression.New(ctor)).Compile();
            ClientboundPacketFactories.Add(key, factory);
            PacketAttributes.TryAdd(type, attribute);
        }

        foreach (var type in packetTypes.Where(t => typeof(IServerboundPacket).IsAssignableFrom(t)))
        {
            var attribute = type.GetCustomAttribute<Attributes.PacketAttribute>();
            if (attribute != null)
            {
                PacketAttributes.TryAdd(type, attribute);
            }
        }

        // 2. Populate Handler Registrations
        foreach (var handlerType in packetTypes.Where(t => typeof(IPacketHandler).IsAssignableFrom(t)))
        {
            var handledPacketAttributes = handlerType.GetCustomAttributes<Attributes.HandlesPacketAttribute>();
            var registrations = new List<(ProtocolState State, int PacketId)>();

            foreach (var handledAttr in handledPacketAttributes)
            {
                if (PacketAttributes.TryGetValue(handledAttr.PacketType, out var packetAttr))
                {
                    registrations.Add((packetAttr.ProtocolState, packetAttr.PacketId));
                }
                else
                {
                    AnsiConsole.WriteLine(
                        $"Warning: Handler {handlerType.FullName} attempts to handle packet {handledAttr.PacketType.FullName}, but it lacks a [Packet] attribute or wasn't found.");
                }
            }

            HandlerRegistrations.Add(handlerType, registrations);
        }

        AnsiConsole.WriteLine(
            $"Packet Registry Initialized: Found {ClientboundPacketFactories.Count} clientbound packets and {HandlerRegistrations.Count} handlers.");
    }

    public static IClientboundPacket CreateIncomingPacket(ProtocolState state, int packetId)
    {
        return ClientboundPacketFactories.TryGetValue((state, packetId), out var factory) ? factory() : new UnknownPacket();
    }

    public static IEnumerable<(ProtocolState State, int PacketId)> GetHandlerRegistrations(IPacketHandler handler)
    {
        return GetHandlerRegistrations(handler.GetType());
    }

    public static IEnumerable<(ProtocolState State, int PacketId)> GetHandlerRegistrations(Type handlerType)
    {
        if (HandlerRegistrations.TryGetValue(handlerType, out var registrations))
        {
            return registrations;
        }

        AnsiConsole.WriteLine($"Warning: Handler type {handlerType.FullName} not found in registry.");
        return [];
    }

    public static Attributes.PacketAttribute? GetPacketAttribute(IPacket packet)
    {
        return GetPacketAttribute(packet.GetType());
    }

    public static Attributes.PacketAttribute? GetPacketAttribute(Type packetType)
    {
        PacketAttributes.TryGetValue(packetType, out var attribute);
        return attribute;
    }

    public static (ProtocolState State, int PacketId)? GetPacketIdAndState(IPacket packet)
    {
        var attr = GetPacketAttribute(packet);
        if (attr != null)
        {
            return (attr.ProtocolState, attr.PacketId);
        }

        return null;
    }
}
