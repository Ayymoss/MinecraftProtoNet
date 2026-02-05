using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
