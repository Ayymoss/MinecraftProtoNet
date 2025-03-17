using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x02, ProtocolState.Play)]
public class AddExperienceOrbPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; }
    public short Count { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();

        var positionX = buffer.ReadDouble();
        var positionY = buffer.ReadDouble();
        var positionZ = buffer.ReadDouble();
        Position = new Vector3<double>(positionX, positionY, positionZ);

        Count = buffer.ReadSignedShort();
    }
}
