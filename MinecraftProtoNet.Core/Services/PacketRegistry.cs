using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public static class PacketRegistry
{
    private static readonly Dictionary<(ProtocolState State, int PacketId), Func<IClientboundPacket>> ClientboundPacketFactories;
    private static readonly Dictionary<Type, Attributes.PacketAttribute> PacketAttributes;
    private static readonly Dictionary<Type, List<(ProtocolState State, int PacketId)>> HandlerRegistrations;
    private static readonly ILogger Logger = LoggingConfiguration.CreateLogger("PacketRegistry");

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
                Logger.LogWarning("Duplicate packet definition for State={State}, ID=0x{PacketId:X2}. Existing: {ExistingType}, New: {NewType}",
                    key.ProtocolState, key.PacketId, packetFactory.Method.DeclaringType?.FullName, type.FullName);
                continue;
            }

            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                Logger.LogWarning("Packet type {PacketType} does not have a parameterless constructor. Cannot create factory",
                    type.FullName);
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
                    Logger.LogWarning("Handler {HandlerType} attempts to handle packet {PacketType}, but it lacks a [Packet] attribute or wasn't found",
                        handlerType.FullName, handledAttr.PacketType.FullName);
                }
            }

            HandlerRegistrations.Add(handlerType, registrations);
        }

        Logger.LogInformation("Packet Registry initialized: Found {ClientboundCount} clientbound packets and {HandlerCount} handlers",
            ClientboundPacketFactories.Count, HandlerRegistrations.Count);
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

        Logger.LogWarning("Handler type {HandlerType} not found in registry", handlerType.FullName);
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
