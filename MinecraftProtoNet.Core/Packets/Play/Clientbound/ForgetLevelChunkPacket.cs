using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x25, ProtocolState.Play, true)]
public class ForgetLevelChunkPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadSignedInt();
        ChunkZ = buffer.ReadSignedInt();
    }
}
