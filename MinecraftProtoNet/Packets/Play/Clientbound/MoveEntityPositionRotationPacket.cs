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
    public short DeltaXRaw { get; set; }
    public short DeltaYRaw { get; set; }
    public short DeltaZRaw { get; set; }
    public double DeltaX => DeltaXRaw / 4096.0;
    public double DeltaY => DeltaYRaw / 4096.0;
    public double DeltaZ => DeltaZRaw / 4096.0;
    public Vector<byte> Yaw { get; set; }
    public Vector<byte> Pitch { get; set; }
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        DeltaXRaw = buffer.ReadSignedShort();
        DeltaYRaw = buffer.ReadSignedShort();
        DeltaZRaw = buffer.ReadSignedShort();
        Yaw = new Vector<byte>(buffer.ReadUnsignedByte());
        Pitch = new Vector<byte>(buffer.ReadUnsignedByte());
        OnGround = buffer.ReadBoolean();
    }
}
