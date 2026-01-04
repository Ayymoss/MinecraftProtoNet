using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
