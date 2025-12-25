using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x01, ProtocolState.Play, true)]
public class AddEntityPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Guid EntityUuid { get; set; }
    public int Type { get; set; }
    public Vector3<double> Position { get; set; }
    public Vector3<double> Velocity { get; set; }
    public sbyte Pitch { get; set; }
    public sbyte Yaw { get; set; }
    public sbyte HeadYaw { get; set; }
    public int Data { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        EntityUuid = buffer.ReadUuid();
        Type = buffer.ReadVarInt();
        Position = new Vector3<double>(buffer.ReadDouble(), buffer.ReadDouble(), buffer.ReadDouble());
        Velocity = buffer.ReadLpVec3();
        Pitch = buffer.ReadSignedByte();
        Yaw = buffer.ReadSignedByte();
        HeadYaw = buffer.ReadSignedByte();
        Data = buffer.ReadVarInt();
    }
}

