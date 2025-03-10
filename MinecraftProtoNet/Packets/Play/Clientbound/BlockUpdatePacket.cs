using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x09, ProtocolState.Play)]
public class BlockUpdatePacket : IClientPacket
{
    public Vector3 Position { get; set; }
    public int BlockId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadAsPosition();
        BlockId = buffer.ReadVarInt();
    }
}
