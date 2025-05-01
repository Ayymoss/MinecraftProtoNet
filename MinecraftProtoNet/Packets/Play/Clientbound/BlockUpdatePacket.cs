using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x08, ProtocolState.Play)]
public class BlockUpdatePacket : IClientboundPacket
{
    public Vector3<double> Position { get; set; }
    public int BlockId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadCoordinatePosition();
        BlockId = buffer.ReadVarInt();
    }
}
