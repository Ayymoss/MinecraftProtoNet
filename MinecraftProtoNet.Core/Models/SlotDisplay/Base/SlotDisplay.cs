namespace MinecraftProtoNet.Core.Models.SlotDisplay.Base;

public class SlotDisplay
{
    public SlotDisplayType Type { get; set; }
    public required SlotDisplayBase Display { get; set; }
}
