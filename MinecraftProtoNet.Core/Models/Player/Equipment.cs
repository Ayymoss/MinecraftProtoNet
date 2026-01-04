using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace MinecraftProtoNet.Core.Models.Player;

public class Equipment
{
    public EquipmentSlot Slot { get; set; }
    public required Slot Item { get; set; }

    public override string ToString()
    {
        return $"{Slot}: {Item}";
    }
}
