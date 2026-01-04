using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Contains the command tree from the server. This is a complex packet with
/// a tree structure of command nodes. For now, we consume the data but don't
/// fully parse the tree structure.
/// </summary>
[Packet(0x10, ProtocolState.Play)]
public class CommandsPacket : IClientboundPacket
{
    public int RootIndex { get; set; }
    public int NodeCount { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Read number of nodes
        NodeCount = buffer.ReadVarInt();

        // Read each node (consume the data)
        for (var i = 0; i < NodeCount; i++)
        {
            ReadNode(ref buffer);
        }

        // Read root index
        RootIndex = buffer.ReadVarInt();
    }

    private static void ReadNode(ref PacketBufferReader buffer)
    {
        var flags = buffer.ReadUnsignedByte();
        var nodeType = flags & 0x03;
        var hasRedirect = (flags & 0x08) != 0;
        var hasSuggestionsType = (flags & 0x10) != 0;

        // Read children array (VarInt array)
        var childCount = buffer.ReadVarInt();
        for (var i = 0; i < childCount; i++)
        {
            buffer.ReadVarInt(); // child index
        }

        // Read redirect node if present
        if (hasRedirect)
        {
            buffer.ReadVarInt(); // redirect index
        }

        // Node type specific data
        switch (nodeType)
        {
            case 1: // Literal node
                buffer.ReadString(); // name
                break;
            case 2: // Argument node
                buffer.ReadString(); // name
                ReadArgumentParser(ref buffer);
                if (hasSuggestionsType)
                {
                    buffer.ReadString(); // suggestions type identifier
                }
                break;
            // type 0 = root, no additional data
        }
    }

    private static void ReadArgumentParser(ref PacketBufferReader buffer)
    {
        var parserId = buffer.ReadVarInt();

        // Different parsers have different properties
        // This is a simplified implementation that handles common cases
        // Reference: https://wiki.vg/Command_Data
        switch (parserId)
        {
            case 1: // brigadier:float
                var floatFlags = buffer.ReadUnsignedByte();
                if ((floatFlags & 0x01) != 0) buffer.ReadFloat(); // min
                if ((floatFlags & 0x02) != 0) buffer.ReadFloat(); // max
                break;
            case 2: // brigadier:double
                var doubleFlags = buffer.ReadUnsignedByte();
                if ((doubleFlags & 0x01) != 0) buffer.ReadDouble(); // min
                if ((doubleFlags & 0x02) != 0) buffer.ReadDouble(); // max
                break;
            case 3: // brigadier:integer
                var intFlags = buffer.ReadUnsignedByte();
                if ((intFlags & 0x01) != 0) buffer.ReadSignedInt(); // min
                if ((intFlags & 0x02) != 0) buffer.ReadSignedInt(); // max
                break;
            case 4: // brigadier:long
                var longFlags = buffer.ReadUnsignedByte();
                if ((longFlags & 0x01) != 0) buffer.ReadSignedLong(); // min
                if ((longFlags & 0x02) != 0) buffer.ReadSignedLong(); // max
                break;
            case 5: // brigadier:string
                buffer.ReadVarInt(); // string type (0=SINGLE_WORD, 1=QUOTABLE_PHRASE, 2=GREEDY_PHRASE)
                break;
            case 6: // minecraft:entity
                buffer.ReadUnsignedByte(); // flags
                break;
            case 29: // minecraft:score_holder
                buffer.ReadUnsignedByte(); // flags
                break;
            case 40: // minecraft:resource_or_tag
            case 41: // minecraft:resource_or_tag_key
            case 42: // minecraft:resource
            case 43: // minecraft:resource_key
                buffer.ReadString(); // registry
                break;
            // Many other parsers have no additional properties
        }
    }
}
