using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Status.Serverbound;

[Packet(0x00, ProtocolState.Status)]
public class StatusRequestPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
