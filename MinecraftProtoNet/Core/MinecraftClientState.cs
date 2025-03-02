using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Core;

public class MinecraftClientState
{
    public int EntityId { get; set; }
    public Vector3F Position { get; set; } = new();
    public Vector3F Velocity { get; set; } = new();
    public Vector2F Rotation { get; set; } = new();
}
