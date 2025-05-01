﻿using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public static class PacketHelper
{
    public static T GetPacketAttributeValue<T>(this IPacket packet, Expression<Func<PacketAttribute, T>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(propertySelector);

        var packetType = packet.GetType();
        var attribute = PacketRegistry.GetPacketAttribute(packetType);

        if (attribute == null)
        {
            throw new InvalidOperationException($"Packet type '{packetType.FullName}' does not have a PacketAttribute.");
        }

        var propertyName = GetPropertyName(propertySelector);
        var propertyInfo = typeof(PacketAttribute).GetProperty(propertyName);

        if (propertyInfo == null || !propertyInfo.CanRead)
        {
            throw new ArgumentException($"Property '{propertyName}' not found or not readable on PacketAttribute.",
                nameof(propertySelector));
        }

        var value = propertyInfo.GetValue(attribute);
        ArgumentNullException.ThrowIfNull(value);

        return (T)value;
    }

    private static string GetPropertyName<T>(Expression<Func<PacketAttribute, T>> propertySelector)
    {
        if (propertySelector.Body is MemberExpression memberExpression) return memberExpression.Member.Name;
        throw new ArgumentException("Invalid property selector expression. Must be a MemberExpression.", nameof(propertySelector));
    }

    public static string GetPropertiesAsString(this IPacket packet)
    {
        var properties = packet.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        var first = true;

        foreach (var property in properties)
        {
            if (!first) sb.Append(", ");
            first = false;

            sb.Append(property.Name);
            sb.Append(": ");
            var value = property.GetValue(packet);
            sb.Append(GetValueAsString(value));
        }

        return sb.ToString();
    }

    private static string? GetValueAsString(object? value)
    {
        switch (value)
        {
            case null:
                return "<NULL>";
            case string stringValue:
                return $"\"{stringValue}\"";
        }

        if (value is not IEnumerable enumerableValue) return value.ToString();

        var sb = new StringBuilder("[");
        var firstItem = true;
        var count = 0;
        foreach (var item in enumerableValue)
        {
            if (!firstItem) sb.Append(", ");
            firstItem = false;

            if (count >= 10)
            {
                sb.Append("...");
                break;
            }

            sb.Append(GetValueAsString(item));
            count++;
        }

        sb.Append(']');
        return sb.ToString();
    }
}
