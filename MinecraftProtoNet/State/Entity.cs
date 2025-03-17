using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.State;

public class Entity
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();
    public int BlockPlaceSequence { get; set; }
}
