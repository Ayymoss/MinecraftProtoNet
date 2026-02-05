using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x34, ProtocolState.Play, true)]
public class MoveEntityPositionPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public short DeltaXRaw { get; set; }
    public short DeltaYRaw { get; set; }
    public short DeltaZRaw { get; set; }
    public Vector3<double> Delta => new(DeltaXRaw / 4096d, DeltaYRaw / 4096d, DeltaZRaw / 4096d);
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        DeltaXRaw = buffer.ReadSignedShort();
        DeltaYRaw = buffer.ReadSignedShort();
        DeltaZRaw = buffer.ReadSignedShort();
        OnGround = buffer.ReadBoolean();
    }
}
