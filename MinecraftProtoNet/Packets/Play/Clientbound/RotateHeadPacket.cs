using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x4C, ProtocolState.Play, true)]
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
