using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x2D, ProtocolState.Play)]
public class LevelEventPacket : IClientboundPacket
{
    public int EventId { get; set; }
    public required Vector3<double>  Position { get; set; }
    public int Data { get; set; }
    public bool DisableRelativeVolume { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EventId = buffer.ReadSignedInt();
        Position = buffer.ReadCoordinatePosition();
        Data = buffer.ReadSignedInt();
        DisableRelativeVolume = buffer.ReadBoolean();
    }
}
