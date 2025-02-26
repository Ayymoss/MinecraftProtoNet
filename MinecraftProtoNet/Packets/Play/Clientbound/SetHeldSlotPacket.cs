using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class SetHeldSlotPacket : Packet
{
    public override int PacketId => 0x63;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public int HandHeldSlot { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        HandHeldSlot = buffer.ReadVarInt();
    }
}
