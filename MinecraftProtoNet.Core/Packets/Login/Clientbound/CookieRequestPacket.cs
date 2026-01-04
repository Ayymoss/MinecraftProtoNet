using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

[Packet(0x05, ProtocolState.Login)]
public class CookieRequestPacket : IClientboundPacket
{
    public required string Key { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Key = buffer.ReadString();
    }
}
