using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x11, ProtocolState.Play)]
public class ClickContainerPacket : IServerboundPacket
{
    public int WindowId { get; set; }
    public int StateId { get; set; }
    public short Slot { get; set; }
    public sbyte Button { get; set; }
    public ClickContainerMode Mode { get; set; }
    public Dictionary<short, Slot> ChangedSlots { get; set; } = new();
    public Slot CarriedItem { get; set; } = Base.Definitions.Slot.Empty;

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(WindowId);
        buffer.WriteVarInt(StateId);
        buffer.WriteSignedShort(Slot);
        buffer.WriteUnsignedByte((byte)Button);
        buffer.WriteVarInt((int)Mode);
        
        buffer.WriteVarInt(ChangedSlots.Count);
        foreach (var kvp in ChangedSlots)
        {
            buffer.WriteSignedShort(kvp.Key);
            kvp.Value.WriteHashed(ref buffer);
        }
        
        CarriedItem.WriteHashed(ref buffer);
    }
}

public enum ClickContainerMode
{
    Pickup = 0,
    QuickMove = 1,
    Swap = 2,
    Clone = 3,
    Throw = 4,
    QuickCraft = 5,
    PickupAll = 6
}
