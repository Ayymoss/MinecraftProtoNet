using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x14, ProtocolState.Play)]
public class ContainerSetSlotPacket : IClientboundPacket
{
    public int WindowId { get; set; }
    public int StateId { get; set; }
    public short SlotToUpdate { get; set; }
    public required Slot Slot { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        WindowId = buffer.ReadVarInt();
        StateId = buffer.ReadVarInt();
        SlotToUpdate = buffer.ReadSignedShort();

        Slot = Slot.Read(ref buffer);
    }
}
