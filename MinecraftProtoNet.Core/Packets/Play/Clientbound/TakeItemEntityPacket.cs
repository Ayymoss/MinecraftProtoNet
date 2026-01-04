using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
