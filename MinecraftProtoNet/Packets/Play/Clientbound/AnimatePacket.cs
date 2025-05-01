using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x02, ProtocolState.Play)]
public class AnimatePacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Animation Animation { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Animation = (Animation)buffer.ReadUnsignedByte();
    }
}
