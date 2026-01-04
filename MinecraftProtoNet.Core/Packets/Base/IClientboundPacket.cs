using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Base;

public interface IClientboundPacket : IPacket
{
    void Deserialize(ref PacketBufferReader buffer);
}
