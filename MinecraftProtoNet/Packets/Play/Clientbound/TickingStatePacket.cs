using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x7E, ProtocolState.Play)]
public class TickingStatePacket : IClientboundPacket
{
    public float TickRate { get; set; }
    public bool IsFrozen { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        TickRate = buffer.ReadFloat();
        IsFrozen = buffer.ReadBoolean();
    }
}
