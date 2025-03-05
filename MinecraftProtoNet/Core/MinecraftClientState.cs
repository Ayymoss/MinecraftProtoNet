using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Core;

public class MinecraftClientState
{
    public int EntityId { get; set; }
    public Vector3D Position { get; set; } = new();
    public Vector3D Velocity { get; set; } = new();
    public Vector2D YawPitch { get; set; } = new();
}
