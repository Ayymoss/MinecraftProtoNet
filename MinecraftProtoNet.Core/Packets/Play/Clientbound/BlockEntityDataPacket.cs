using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sent when a block entity's NBT data is updated (e.g., sign text changes).
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundBlockEntityDataPacket.java
/// </summary>
[Packet(0x06, ProtocolState.Play)]
public class BlockEntityDataPacket : IClientboundPacket
{
    public Vector3<int> Position { get; set; }
    public int BlockEntityType { get; set; }
    public NbtTag? Nbt { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadBlockPos();
        BlockEntityType = buffer.ReadVarInt();
        Nbt = buffer.ReadNbtTag();
    }
}
