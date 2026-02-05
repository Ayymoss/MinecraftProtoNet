using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x5D, ProtocolState.Play)]
public class SetChunkCacheCenterPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadVarInt();
        ChunkZ = buffer.ReadVarInt();
    }
}
