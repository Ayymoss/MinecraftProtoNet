using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x78, ProtocolState.Play)]
public class TickingStatePacket : IClientPacket
{
    public float TickRate { get; set; }
    public bool IsFrozen { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        TickRate = buffer.ReadFloat();
        IsFrozen = buffer.ReadBoolean();
    }
}
