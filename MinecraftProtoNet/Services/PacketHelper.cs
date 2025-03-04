using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public static class PacketHelper
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> PropertyInfoCache = new();

    private static readonly ConcurrentDictionary<Type, PacketAttribute> PacketAttributeCache = new();

    private static readonly ConcurrentDictionary<(IPacket, string), object> PacketValueCache = new();

    public static T GetPacketAttributeValue<T>(this IPacket packet, Expression<Func<PacketAttribute, T>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(propertySelector);

        var propertyName = GetPropertyName(propertySelector);
        if (PacketValueCache.TryGetValue((packet, propertyName), out var cachedValue)) return (T)cachedValue;

        var packetType = packet.GetType();
        var attribute = PacketAttributeCache.GetOrAdd(packetType, type =>
        {
            var attr = type.GetCustomAttribute<PacketAttribute>();
            if (attr == null)
            {
                throw new InvalidOperationException($"Packet type '{type.FullName}' does not have a PacketAttribute.");
            }

            return attr;
        });

        var propertyInfo = PropertyInfoCache.GetOrAdd((packetType, propertyName), key =>
        {
            var propInfo = typeof(PacketAttribute).GetProperty(key.Item2);
            if (propInfo == null || !propInfo.CanRead)
            {
                throw new ArgumentException($"Property '{key.Item2}' not found or not readable on PacketAttribute.",
                    nameof(propertySelector));
            }

            return propInfo;
        });

        var value = propertyInfo.GetValue(attribute);
        ArgumentNullException.ThrowIfNull(value);
        PacketValueCache.TryAdd((packet, propertyName), value);
        return (T)value;
    }

    private static string GetPropertyName<T>(Expression<Func<PacketAttribute, T>> propertySelector)
    {
        if (propertySelector.Body is MemberExpression memberExpression) return memberExpression.Member.Name;
        throw new ArgumentException("Invalid property selector expression.  Must be a MemberExpression.", nameof(propertySelector));
    }
}
