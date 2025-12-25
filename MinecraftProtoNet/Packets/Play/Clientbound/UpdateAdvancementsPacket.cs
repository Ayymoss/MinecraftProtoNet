using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Updates advancement data. This is a complex packet with advancement trees.
/// For now, we parse the basic structure but consume most complex nested data.
/// </summary>
[Packet(0x81, ProtocolState.Play)]
public class UpdateAdvancementsPacket : IClientboundPacket
{
    public bool Reset { get; set; }
    public bool ShowAdvancements { get; set; }
    public int AddedCount { get; set; }
    public int RemovedCount { get; set; }
    public int ProgressCount { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Reset = buffer.ReadBoolean();

        // Read added advancements
        AddedCount = buffer.ReadVarInt();
        for (var i = 0; i < AddedCount; i++)
        {
            ReadAdvancementHolder(ref buffer);
        }

        // Read removed advancements (list of identifiers)
        RemovedCount = buffer.ReadVarInt();
        for (var i = 0; i < RemovedCount; i++)
        {
            buffer.ReadString(); // advancement identifier
        }

        // Read progress map
        ProgressCount = buffer.ReadVarInt();
        for (var i = 0; i < ProgressCount; i++)
        {
            buffer.ReadString(); // advancement identifier
            ReadAdvancementProgress(ref buffer);
        }

        ShowAdvancements = buffer.ReadBoolean();
    }

    private static void ReadAdvancementHolder(ref PacketBufferReader buffer)
    {
        buffer.ReadString(); // advancement identifier

        // Read advancement data
        var hasParent = buffer.ReadBoolean();
        if (hasParent)
        {
            buffer.ReadString(); // parent identifier
        }

        var hasDisplay = buffer.ReadBoolean();
        if (hasDisplay)
        {
            ReadAdvancementDisplay(ref buffer);
        }

        // Read requirements (2D array of strings)
        var requirementsCount = buffer.ReadVarInt();
        for (var i = 0; i < requirementsCount; i++)
        {
            var innerCount = buffer.ReadVarInt();
            for (var j = 0; j < innerCount; j++)
            {
                buffer.ReadString(); // criterion name
            }
        }

        buffer.ReadBoolean(); // sends telemetry event
    }

    private static void ReadAdvancementDisplay(ref PacketBufferReader buffer)
    {
        buffer.ReadNbtTag(); // title (Chat component as NBT)
        buffer.ReadNbtTag(); // description (Chat component as NBT)

        // Read icon (Slot)
        ReadSlot(ref buffer);

        buffer.ReadVarInt(); // frame type
        var flags = buffer.ReadSignedInt();

        if ((flags & 0x01) != 0)
        {
            buffer.ReadString(); // background texture
        }

        buffer.ReadFloat(); // x coordinate
        buffer.ReadFloat(); // y coordinate
    }

    private static void ReadSlot(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        if (count <= 0) return;

        buffer.ReadVarInt(); // item id

        // Read components added
        var addedCount = buffer.ReadVarInt();
        // Read components removed
        var removedCount = buffer.ReadVarInt();

        // For now, skip component data - this is very complex
        // We would need to read structured component data here
        // Just consume remaining bytes for this slot
        for (var i = 0; i < addedCount; i++)
        {
            buffer.ReadVarInt(); // component type
            // Component data varies by type - skip for now by reading NBT
            buffer.ReadNbtTag();
        }

        for (var i = 0; i < removedCount; i++)
        {
            buffer.ReadVarInt(); // component type to remove
        }
    }

    private static void ReadAdvancementProgress(ref PacketBufferReader buffer)
    {
        var criteriaCount = buffer.ReadVarInt();
        for (var i = 0; i < criteriaCount; i++)
        {
            buffer.ReadString(); // criterion identifier

            var hasAchievedDate = buffer.ReadBoolean();
            if (hasAchievedDate)
            {
                buffer.ReadSignedLong(); // date achieved (epoch millis)
            }
        }
    }
}
