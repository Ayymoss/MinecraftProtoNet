using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x60, ProtocolState.Play)]
public class SetDefaultSpawnPositionPacket : IClientboundPacket
{
    public required Vector3<double> Location { get; set; }
    public float Angle { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Location = buffer.ReadCoordinatePosition();
        Angle = buffer.ReadFloat();
    }
}
