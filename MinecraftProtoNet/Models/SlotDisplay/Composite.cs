using MinecraftProtoNet.Models.SlotDisplay.Base;

namespace MinecraftProtoNet.Models.SlotDisplay;

public class Composite : SlotDisplayBase
{
    public List<SlotDisplayBase> SlotDisplayBases { get; set; }}
