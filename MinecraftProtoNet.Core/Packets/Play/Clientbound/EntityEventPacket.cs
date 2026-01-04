using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

// TODO: Partially implemented.
[Packet(0x22, ProtocolState.Play, true)]
public class EntityEventPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public sbyte EntityStatus { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadSignedInt();
        EntityStatus = buffer.ReadSignedByte();
    }
}
