using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

[Packet(0x03, ProtocolState.Login)]
public class LoginCompressionPacket : IClientboundPacket
{
    public int Threshold { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Threshold = buffer.ReadVarInt();
    }
}
