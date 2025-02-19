using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IIncomingPacket
{
    public int PacketId { get; }
    public PacketDirection Direction { get; }
    void Deserialize(ref PacketBufferReader buffer);
}
