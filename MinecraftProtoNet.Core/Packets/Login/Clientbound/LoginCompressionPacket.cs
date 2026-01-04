using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

[Packet(0x03, ProtocolState.Login)]
public class LoginCompressionPacket : IClientboundPacket
{
    public int Threshold { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Threshold = buffer.ReadVarInt();
    }
}
