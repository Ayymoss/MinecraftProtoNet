using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
