using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace MinecraftProtoNet.Models.Player;

public class Equipment
{
    public EquipmentSlot Slot { get; set; }
    public Slot Item { get; set; }

    public override string ToString()
    {
        return $"{Slot}: {Item}";
    }
}
