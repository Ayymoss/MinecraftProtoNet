using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IClientPacket : IPacket
{
    void Deserialize(ref PacketBufferReader buffer);
}
