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

    /// <summary>
    /// Requires calling base at the START of the method for PacketId serialisation.
    /// </summary>
    /// <param name="buffer"></param>
    public virtual void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
    }
}
