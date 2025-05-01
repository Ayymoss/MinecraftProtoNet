using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IServerboundPacket : IPacket
{
    void Serialize(ref PacketBufferWriter buffer);
}
