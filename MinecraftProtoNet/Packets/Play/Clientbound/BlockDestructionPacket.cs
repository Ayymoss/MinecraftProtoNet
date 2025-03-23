using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x06, ProtocolState.Play)]
public class BlockDestructionPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Vector3<double> Location { get; set; }
    public byte Stage { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Location = buffer.ReadCoordinatePosition();
        Stage = buffer.ReadUnsignedByte();
    }
}
