using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Base.Definitions;

/// <summary>
/// Represents an item cost for merchant trading.
/// Based on ItemCost.java from Minecraft source.
/// </summary>
public record ItemCost(int ItemId, int Count)
{
    /// <summary>
    /// Reads an ItemCost from the packet buffer.
    /// Format: Item (VarInt registry ID) + Count (VarInt) + DataComponentExactPredicate (list of TypedDataComponent)
    /// </summary>
    public static ItemCost Read(ref PacketBufferReader reader)
    {
        var itemId = reader.ReadVarInt();
        var count = reader.ReadVarInt();
        
        // DataComponentExactPredicate - encoded as list of TypedDataComponent
        // Format: VarInt count + (for each: VarInt typeId + component data based on type)
        var predicateCount = reader.ReadVarInt();
        for (var i = 0; i < predicateCount; i++)
        {
            var typeId = reader.ReadVarInt();
            var type = (ComponentType)typeId;
            // Skip component data - reuse Slot's ReadComponentData which just discards it
            Slot.ReadComponentData(ref reader, type, typeId);
        }
        
        return new ItemCost(itemId, count);
    }




    /// <summary>
    /// Reads an optional ItemCost (prefixed with boolean presence flag).
    /// </summary>
    public static ItemCost? ReadOptional(ref PacketBufferReader reader)
    {
        return reader.ReadBoolean() ? Read(ref reader) : null;
    }

    /// <summary>
    /// Writes an ItemCost to the packet buffer.
    /// </summary>
    public void Write(ref PacketBufferWriter writer)
    {
        writer.WriteVarInt(ItemId);
        writer.WriteVarInt(Count);
        writer.WriteVarInt(0); // Empty predicate map
    }

    /// <summary>
    /// Writes an optional ItemCost (with boolean presence flag).
    /// </summary>
    public static void WriteOptional(ref PacketBufferWriter writer, ItemCost? cost)
    {
        if (cost is null)
        {
            writer.WriteBoolean(false);
        }
        else
        {
            writer.WriteBoolean(true);
            cost.Write(ref writer);
        }
    }
}
