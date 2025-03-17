using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x29, ProtocolState.Play)]
public class LevelEventPacket : IClientPacket
{
    public int EventId { get; set; }
    public Vector3<double>  Position { get; set; }
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
