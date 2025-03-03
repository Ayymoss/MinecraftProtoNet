using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IServerPacket : IPacket
{
    void Serialize(ref PacketBufferWriter buffer);
}
