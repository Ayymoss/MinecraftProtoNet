using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x5E, ProtocolState.Play)]
public class SetChunkCacheRadiusPacket : IClientboundPacket
{
    public int ViewDistance { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ViewDistance = buffer.ReadVarInt();
    }
}
