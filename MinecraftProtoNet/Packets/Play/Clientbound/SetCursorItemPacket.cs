using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x5F, ProtocolState.Play)]
public class SetCursorItemPacket : IClientboundPacket
{
    public required Slot Contents { get; set; }

    public void Deserialize(ref PacketBufferReader reader)
    {
        Contents = Slot.Read(ref reader);
    }
}
