using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

[Packet(0x05, ProtocolState.Login)]
public class CookieRequestPacket : IClientboundPacket
{
    public required string Key { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Key = buffer.ReadString();
    }
}
