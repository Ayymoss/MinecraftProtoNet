using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x31, ProtocolState.Play, true)]
public class MoveEntityRotationPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public sbyte Yaw { get; set; }
    public sbyte Pitch { get; set; }
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Yaw = buffer.ReadSignedByte();
        Pitch = buffer.ReadSignedByte();
        OnGround = buffer.ReadBoolean();
    }
}
