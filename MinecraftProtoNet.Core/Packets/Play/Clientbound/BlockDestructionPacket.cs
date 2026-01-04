using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x05, ProtocolState.Play)]
public class BlockDestructionPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public required Vector3<double> Location { get; set; }
    public byte Stage { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Location = buffer.ReadCoordinatePosition();
        Stage = buffer.ReadUnsignedByte();
    }
}
