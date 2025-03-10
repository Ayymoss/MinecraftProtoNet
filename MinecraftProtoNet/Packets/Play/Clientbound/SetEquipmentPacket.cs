using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

// TODO: Implement this packet
[Packet(0x60, ProtocolState.Play)]
public class SetEquipmentPacket : IClientPacket
{
    public int EntityId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // ARRAY: The length of the array is unknown, it must be read until the most significant bit is 1 ((Slot >>> 7 & 1) == 1)
        // SLOT: Equipment slot (see below). Also has the top bit set if another entry follows, and otherwise unset if this is the last item in the array.
    }

    public class Equipment
    {
        public EquipmentSlot Slot { get; set; }
        public Slot Item { get; set; }
    }
}
