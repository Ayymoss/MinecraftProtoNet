using System.Collections.Concurrent;
using System.Reflection;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public static class PacketHelper
{
    private static readonly ConcurrentDictionary<IPacket, int> PacketIdCache = [];
    private static readonly ConcurrentDictionary<IPacket, bool> PacketSilentCache = [];

    public static int GetPacketId(this IPacket packet)
    {
        if (PacketIdCache.TryGetValue(packet, out var packetId)) return packetId;

        var type = packet.GetType();
        var attribute = type.GetCustomAttribute<PacketAttribute>();
        if (attribute is null) throw new InvalidOperationException($"Packet type '{type.FullName}' does not have a PacketAttribute.");

        packetId = attribute.PacketId;
        PacketIdCache.TryAdd(packet, packetId);
        return packetId;
    }

    public static bool GetPacketSilentState(this IPacket packet)
    {
        if (PacketSilentCache.TryGetValue(packet, out var isSilent)) return isSilent;

        var type = packet.GetType();
        var attribute = type.GetCustomAttribute<PacketAttribute>();
        if (attribute is null) throw new InvalidOperationException($"Packet type '{type.FullName}' does not have a PacketAttribute.");

        isSilent = attribute.Silent;
        PacketSilentCache.TryAdd(packet, isSilent);
        return isSilent;
    }
}
