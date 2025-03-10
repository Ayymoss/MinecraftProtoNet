namespace MinecraftProtoNet.Packets.Base.Definitions;

public class Slot
{
    // TODO: Partial: https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/Slot_Data
    public int ItemCount { get; set; }
    public int? ItemId { get; set; }
    public int? ComponentsToAdd { get; set; }
    public int? ComponentsToRemove { get; set; }
}
