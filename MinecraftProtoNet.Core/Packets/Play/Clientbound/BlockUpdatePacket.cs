using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x08, ProtocolState.Play)]
public class BlockUpdatePacket : IClientboundPacket
{
    public required Vector3<double> Position { get; set; }
    public int BlockId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadCoordinatePosition();
        BlockId = buffer.ReadVarInt();
    }
}
