using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x7B, ProtocolState.Play)]
public class TakeItemEntityPacket : IClientboundPacket
{
    public int CollectedEntityId { get; set; }
    public int CollectorEntityId { get; set; }
    public int PickupItemCount { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        CollectedEntityId = buffer.ReadVarInt();
        CollectorEntityId = buffer.ReadVarInt();
        PickupItemCount = buffer.ReadVarInt();
    }
}
