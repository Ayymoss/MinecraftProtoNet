using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x12, ProtocolState.Play)]
public class ContainerSetContentPacket : IClientboundPacket
{
    public int WindowId { get; set; }
    public int StateId { get; set; }
    public required Slot[] SlotData { get; set; }
    public required Slot CarriedItem { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        WindowId = buffer.ReadVarInt();
        StateId = buffer.ReadVarInt();

        SlotData = new Slot[buffer.ReadVarInt()];
        for (var i = 0; i < SlotData.Length; i++)
        {
            SlotData[i] = Slot.Read(ref buffer);
        }

        CarriedItem = Slot.Read(ref buffer);
    }
}
