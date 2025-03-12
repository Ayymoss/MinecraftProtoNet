using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.State;

public class Player
{
    public Guid Uuid { get; set; }
    public int EntityId { get; set; }
    public string Username { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2D YawPitch { get; set; } = new();
    public List<object?> Objects { get; set; } // TODO: This needs to typed properly.

    public override string ToString()
    {
        return $"({EntityId}) Player: {Username} ({Uuid}) Position: {Position} Velocity: {Velocity} YawPitch: {YawPitch} Objects: {Objects.Count}";
    }
}
