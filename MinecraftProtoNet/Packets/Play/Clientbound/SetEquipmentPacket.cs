using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Player;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

// TODO: Implement this packet
[Packet(0x65, ProtocolState.Play)]
public class SetEquipmentPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Equipment[] Equipment { get; set; }

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
