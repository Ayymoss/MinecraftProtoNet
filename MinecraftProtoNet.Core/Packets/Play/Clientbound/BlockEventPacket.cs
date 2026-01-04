using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Block event packet for block animations (chest lid, note block, etc.).
/// Used for visual updates only - we just need to consume it to prevent warnings.
/// </summary>
[Packet(0x07, ProtocolState.Play, true)]
public class BlockEventPacket : IClientboundPacket
{
    /// <summary>
    /// The block position (encoded as long).
    /// </summary>
    public long Position { get; set; }
    
    /// <summary>
    /// The action ID (block-specific).
    /// For chests: 1 = players viewing count changed
    /// </summary>
    public byte ActionId { get; set; }
    
    /// <summary>
    /// The action parameter (block-specific).
    /// For chests: number of players viewing
    /// </summary>
    public byte ActionParam { get; set; }
    
    /// <summary>
    /// The block type registry ID.
    /// </summary>
    public int BlockType { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadSignedLong();
        ActionId = buffer.ReadUnsignedByte();
        ActionParam = buffer.ReadUnsignedByte();
        BlockType = buffer.ReadVarInt();
    }
}
