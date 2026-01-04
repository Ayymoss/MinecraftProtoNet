using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x37, ProtocolState.Play, true)]
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
