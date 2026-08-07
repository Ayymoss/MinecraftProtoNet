using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Player;
using MinecraftProtoNet.Core.NBT.Tags;

namespace MinecraftProtoNet.Core.State;

public class Player
{
    public bool IsFullyRegistered => HasEntity && HasUsername;

    [MemberNotNullWhen(true, nameof(Username))]
    public bool HasUsername => Username is not null;

    [MemberNotNullWhen(true, nameof(Entity))]
    public bool HasEntity => Entity is not null;

    public string? Username { get; set; }
    public Guid Uuid { get; set; }
    public GameMode GameMode { get; set; }
    public int Latency { get; set; }
    public List<Property> Properties { get; set; } = [];

    /// <summary>
    /// Tab-list display name (PlayerInfoUpdate UPDATE_DISPLAY_NAME). Servers use this for rank prefixes and,
    /// on hub servers, to label NPC-backing fake players whose <see cref="Username"/> is a meaningless id.
    /// Null when the server sends no override, in which case the username is displayed.
    /// </summary>
    public NbtTag? DisplayName { get; set; }

    /// <summary>
    /// Whether this entry is shown in the tab list (UPDATE_LISTED). NPC-backing profiles are usually unlisted.
    /// </summary>
    public bool IsListed { get; set; } = true;

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
