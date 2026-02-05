using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Player;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

// TODO: Implement this packet
[Packet(0x65, ProtocolState.Play)]
public class SetEquipmentPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public required Equipment[] Equipment { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        var equipmentList = new List<Equipment>();
        while (true)
        {
            var slot = buffer.ReadUnsignedByte();
            var item = Slot.Read(ref buffer);

            if ((slot & 0x80) == 0)
            {
                equipmentList.Add(new Equipment
                {
                    Slot = (EquipmentSlot)(slot & 0x7F),
                    Item = item
                });
                break;
            }

            equipmentList.Add(new Equipment
            {
                Slot = (EquipmentSlot)(slot & 0x7F),
                Item = item
            });
        }

        Equipment = equipmentList.ToArray();
    }
}
