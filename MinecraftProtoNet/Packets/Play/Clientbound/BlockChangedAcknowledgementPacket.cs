using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x05, ProtocolState.Play)]
public class BlockChangedAcknowledgementPacket : IClientPacket
{
    public int Sequence { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Sequence = buffer.ReadVarInt();
    }
}
