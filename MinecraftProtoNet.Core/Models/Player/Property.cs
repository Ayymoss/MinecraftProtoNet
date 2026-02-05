namespace MinecraftProtoNet.Core.Models.Player;

public class Property
{
    public required string Name { get; set; }
    public required string Value { get; set; }
    public string? Signature { get; set; }
}
