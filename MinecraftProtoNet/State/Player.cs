using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Models.Player;

namespace MinecraftProtoNet.State;

public class Player
{
    public bool IsFullyRegistered => HasEntity && HasUsername;

    [MemberNotNullWhen(true, nameof(Username))]
    public bool HasUsername => Username is not null;

    [MemberNotNullWhen(true, nameof(Entity))]
    public bool HasEntity => Entity is not null;

    public string? Username { get; set; }
    public Guid Uuid { get; set; }
    public int GameMode { get; set; }
    public int Latency { get; set; }
    public List<Property> Properties { get; set; } = [];

    /// <summary>
    /// Represents the physical entity in the world.
    /// </summary>
    public Entity? Entity { get; set; }

    public override string ToString()
    {
        return
            $"Player: {Username} ({Uuid}) - {GameMode} - {Latency}ms - {Properties.Count} properties - {HasEntity} - {HasUsername} - {IsFullyRegistered}";
    }
}
