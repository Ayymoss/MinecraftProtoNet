using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x30, ProtocolState.Play)]
public class MoveEntityPositionRotationPacket : IClientPacket
{
    public int EntityId { get; set; }
    public short DeltaX { get; set; }
    public short DeltaY { get; set; }
    public short DeltaZ { get; set; }
    public Vector<byte> Yaw { get; set; }
    public Vector<byte> Pitch { get; set; }
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        DeltaX = buffer.ReadSignedShort();
        DeltaY = buffer.ReadSignedShort();
        DeltaZ = buffer.ReadSignedShort();
        Yaw = new Vector<byte>(buffer.ReadUnsignedByte());
        Pitch = new Vector<byte>(buffer.ReadUnsignedByte());
        OnGround = buffer.ReadBoolean();
    }
}
