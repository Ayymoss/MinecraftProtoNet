using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x0C, ProtocolState.Play, true)]
public class ClientTickEndPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
