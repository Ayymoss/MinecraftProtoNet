using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x5F, ProtocolState.Play)]
public class SetCursorItemPacket : IClientboundPacket
{
    public required Slot Contents { get; set; }

    public void Deserialize(ref PacketBufferReader reader)
    {
        Contents = Slot.Read(ref reader);
    }
}
