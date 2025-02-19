using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public interface IOutgoingPacket
{
    public int PacketId { get; }
    public PacketDirection Direction { get; }
    void Serialize(ref PacketBufferWriter buffer);
}
