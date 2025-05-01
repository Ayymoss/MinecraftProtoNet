using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x58, ProtocolState.Play)]
public class SetChunkCacheRadiusPacket : IClientboundPacket
{
    public int ViewDistance { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ViewDistance = buffer.ReadVarInt();
    }
}
