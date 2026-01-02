using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x7F, ProtocolState.Play)]
public class TickingStepPacket : IClientboundPacket
{
    public int TickSteps { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        TickSteps = buffer.ReadVarInt();
    }
}
