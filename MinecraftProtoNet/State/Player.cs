using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.State;

public class Player
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2D YawPitch { get; set; } = new();
}
