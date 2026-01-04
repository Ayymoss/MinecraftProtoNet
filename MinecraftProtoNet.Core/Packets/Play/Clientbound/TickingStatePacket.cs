using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
