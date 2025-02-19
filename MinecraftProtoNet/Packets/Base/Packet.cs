using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base;

public abstract class Packet : IIncomingPacket, IOutgoingPacket
{
    public abstract int PacketId { get; }
    public abstract PacketDirection Direction { get; }

    public virtual void Deserialize(ref PacketBufferReader buffer)
    {
    }

    public virtual void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
