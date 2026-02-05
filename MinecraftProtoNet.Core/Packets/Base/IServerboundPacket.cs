using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Base;

public interface IServerboundPacket : IPacket
{
    void Serialize(ref PacketBufferWriter buffer);
}
