using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base.Definitions;

public class Slot
{
    // TODO: Partial: https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/Slot_Data
    public int ItemCount { get; set; }
    public int? ItemId { get; set; }
    public StructuredComponent[]? ComponentsToAdd { get; set; }
    public ComponentType[]? ComponentsToRemove { get; set; }

    public static Slot Read(ref PacketBufferReader reader)
    {
        var slot = new Slot
        {
            ItemCount = reader.ReadVarInt()
        };

        if (slot.ItemCount is 0) return slot;

        slot.ItemId = reader.ReadVarInt();
        var componentsToAddCount = reader.ReadVarInt();
        var componentsToRemoveCount = reader.ReadVarInt();

        slot.ComponentsToAdd = new StructuredComponent[componentsToAddCount];
        for (var i = 0; i < componentsToAddCount; i++)
        {
            var type = (ComponentType)reader.ReadVarInt();
            var data = GetStructuredComponentData(ref reader, type);
            slot.ComponentsToAdd[i] = new StructuredComponent
            {
                Type = type,
                Data = data
            };
        }

        slot.ComponentsToRemove = new ComponentType[componentsToRemoveCount];
        for (var i = 0; i < componentsToRemoveCount; i++)
        {
            slot.ComponentsToRemove[i] = (ComponentType)reader.ReadVarInt();
        }

        return slot;

        object? GetStructuredComponentData(ref PacketBufferReader reader, ComponentType type)
        {
            return type switch
            {
                ComponentType.CustomData => reader.ReadNbtTag(),
                _ => null
            };
        }
    }

    public override string ToString()
    {
        return $"{ItemCount} {ItemId?.ToString() ?? "<NULL>"} {ComponentsToAdd?.Length.ToString() ?? "<NULL>"} {ComponentsToRemove?.Length.ToString() ?? "<NULL>"}";
    }
}

public class StructuredComponent
{
    public ComponentType Type { get; set; }
    public object? Data { get; set; }

    public override string ToString()
    {
        return $"{Type} {Data?.GetType().ToString() ?? "<NULL>"}";
    }
}

public enum ComponentType
{
    CustomData = 0,
}
