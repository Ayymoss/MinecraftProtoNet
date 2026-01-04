using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x2B, ProtocolState.Play)]
public class PlayerLoadedPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
