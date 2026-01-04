using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x64, ProtocolState.Play, true)]
public class SetEntityMotionPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public required Vector3<double> Velocity { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Velocity = buffer.ReadLpVec3();
    }
}

