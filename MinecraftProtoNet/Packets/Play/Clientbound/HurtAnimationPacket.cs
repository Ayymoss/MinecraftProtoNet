using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x29, ProtocolState.Play)]
public class HurtAnimationPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public float Yaw { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Yaw = buffer.ReadFloat();
    }
}
