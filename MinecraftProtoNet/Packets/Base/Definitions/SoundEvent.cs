namespace MinecraftProtoNet.Packets.Base.Definitions;

public class SoundEvent
{
    public required string Name { get; set; }
    public bool HasFixedRange { get; set; }
    public float? FixedRange { get; set; }
}
