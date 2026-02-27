using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x80, ProtocolState.Play)]
public class TickingStepPacket : IClientboundPacket
{
    public int TickSteps { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        TickSteps = buffer.ReadVarInt();
    }
}
