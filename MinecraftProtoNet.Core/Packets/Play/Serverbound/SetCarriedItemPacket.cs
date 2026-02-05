using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x34, ProtocolState.Play)]
public class SetCarriedItemPacket : IServerboundPacket
{
    public required short Slot { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteSignedShort(Slot);
    }
}
