using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x4D, ProtocolState.Play, true)]
public class RotateHeadPacket : IClientPacket
{
    public int EntityId { get; set; }
    public sbyte HeadYaw { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        HeadYaw = buffer.ReadSignedByte();
    }
}
