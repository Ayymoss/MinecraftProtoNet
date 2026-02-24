using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x04, ProtocolState.Play, silent: true)]
public class BlockChangedAcknowledgementPacket : IClientboundPacket
{
    public int Sequence { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Sequence = buffer.ReadVarInt();
    }
}
