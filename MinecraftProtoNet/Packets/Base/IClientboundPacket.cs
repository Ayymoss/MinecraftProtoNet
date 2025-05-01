using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IClientboundPacket : IPacket
{
    void Deserialize(ref PacketBufferReader buffer);
}
