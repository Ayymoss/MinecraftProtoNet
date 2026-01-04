using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x1B, ProtocolState.Play, true)]
public class KeepAlivePacket : IServerboundPacket
{
    public required long Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteSignedLong(Payload);
    }
}
