using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x52, ProtocolState.Play, true)]
public class RotateHeadPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public sbyte HeadYaw { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        HeadYaw = buffer.ReadSignedByte();
    }
}
