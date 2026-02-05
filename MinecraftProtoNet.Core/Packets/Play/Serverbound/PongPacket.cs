using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x2C, ProtocolState.Play)]
public class PongPacket : IServerboundPacket
{
    public int Payload { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteSignedInt(Payload);
    }
}
