using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x01, ProtocolState.Play)]
public class AddEntityPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Guid EntityUuid { get; set; }
    public int Type { get; set; }

    public Vector3D Position { get; set; }
    public Vector3D PitchYawAndHeadYaw { get; set; }
    public int Data { get; set; }
    public Vector3D Velocity { get; set; } // TODO: This needs to be converted to Shorts.

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        EntityUuid = buffer.ReadUUID(); // TODO: Upon connecting we get a UUID from everyone.
        Type = buffer.ReadVarInt();
        Position = new Vector3D((float)buffer.ReadDouble(), (float)buffer.ReadDouble(), (float)buffer.ReadDouble());
        PitchYawAndHeadYaw = new Vector3D(buffer.ReadSignedByte(), buffer.ReadSignedByte(), buffer.ReadSignedByte());
        Data = buffer.ReadVarInt();
        Velocity = new Vector3D(buffer.ReadSignedShort(), buffer.ReadSignedShort(), buffer.ReadSignedShort());
    }
}
