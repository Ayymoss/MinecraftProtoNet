using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x64, ProtocolState.Play, true)]
public class SetEntityMotionPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Vector3<double> Velocity { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Velocity = buffer.ReadLpVec3();
    }
}

