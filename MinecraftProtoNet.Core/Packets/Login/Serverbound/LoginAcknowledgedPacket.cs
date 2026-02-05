using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Serverbound;

[Packet(0x03, ProtocolState.Login)]
public class LoginAcknowledgedPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
