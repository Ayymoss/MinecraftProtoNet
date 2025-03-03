using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x2F, ProtocolState.Play)]
public class MoveEntityPositionPacket : IClientPacket
{
    public int EntityId { get; set; }
    public short DeltaX { get; set; }
    public short DeltaY { get; set; }
    public short DeltaZ { get; set; }
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        DeltaX = buffer.ReadSignedShort();
        DeltaY = buffer.ReadSignedShort();
        DeltaZ = buffer.ReadSignedShort();
        OnGround = buffer.ReadBoolean();
    }
}
