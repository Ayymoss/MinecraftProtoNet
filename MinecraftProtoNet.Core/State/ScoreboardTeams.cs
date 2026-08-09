using System.Collections.Concurrent;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// Whether members of a team can be pushed by entity collision.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/Team.java:69-73
/// </summary>
public enum TeamCollisionRule
{
    Always = 0,
    Never = 1,
    PushOtherTeams = 2,
    PushOwnTeam = 3
}

/// <summary>
/// Whether a team's members show a nametag. Parsed for wire-format completeness; not consumed yet.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/Team.java:43-47
/// </summary>
public enum TeamNameTagVisibility
{
    Always = 0,
    Never = 1,
    HideForOtherTeams = 2,
    HideForOwnTeam = 3
}

/// <summary>
/// A scoreboard team as sent by the server.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/PlayerTeam.java
/// </summary>
public class ScoreboardTeam(string name)
{
    public string Name { get; } = name;
    public TeamCollisionRule CollisionRule { get; set; } = TeamCollisionRule.Always;
    public TeamNameTagVisibility NameTagVisibility { get; set; } = TeamNameTagVisibility.Always;

    /// <summary>
    /// Team members. For players this is the username; for other entities, the UUID string.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/PlayerTeam.java (getPlayers)
    /// </summary>
    public HashSet<string> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>Text drawn before a member's name; carries sidebar line content on SkyBlock.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Text drawn after a member's name; carries the rest of the sidebar line.</summary>
    public string Suffix { get; set; } = string.Empty;
}

/// <summary>
/// Client-side mirror of the server scoreboard's teams.
///
/// Exists for one reason that matters to movement: <see cref="TeamCollisionRule"/>. Vanilla gates ALL
/// entity-to-entity pushing on it (<c>EntitySelector.pushableBy</c>), and lobby servers routinely set
/// <c>collisionRule: never</c> so players cannot shove each other on a crowded spawn pad. A client that
/// ignores teams gets pushed around by a crowd the server holds still, and every push is a desync the
/// server has to correct with a teleport.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/Scoreboard.java
/// </summary>
public class TeamRegistry
{
    private readonly ConcurrentDictionary<string, ScoreboardTeam> _teams = new(StringComparer.Ordinal);

    /// <summary>Member name -> team, so a per-tick lookup does not scan every team.</summary>
    private readonly ConcurrentDictionary<string, ScoreboardTeam> _membership = new(StringComparer.Ordinal);

    public ScoreboardTeam GetOrCreate(string name) => _teams.GetOrAdd(name, static n => new ScoreboardTeam(n));

    public void Remove(string name)
    {
        if (!_teams.TryRemove(name, out var team)) return;

        foreach (var member in team.Members)
        {
            // Only drop the mapping if it still points at this team — a member may have already been
            // re-added to a different one.
            if (_membership.TryGetValue(member, out var current) && ReferenceEquals(current, team))
            {
                _membership.TryRemove(member, out _);
            }
        }
    }

    public void AddMembers(string teamName, IEnumerable<string> members)
    {
        var team = GetOrCreate(teamName);
        foreach (var member in members)
        {
            team.Members.Add(member);
            _membership[member] = team;
        }
    }

    public void RemoveMembers(string teamName, IEnumerable<string> members)
    {
        if (!_teams.TryGetValue(teamName, out var team)) return;

        foreach (var member in members)
        {
            team.Members.Remove(member);
            if (_membership.TryGetValue(member, out var current) && ReferenceEquals(current, team))
            {
                _membership.TryRemove(member, out _);
            }
        }
    }

    /// <summary>
    /// The team a member belongs to, or null. Equivalent to Java's <c>Entity.getTeam()</c>.
    /// </summary>
    public ScoreboardTeam? GetTeamOf(string? member)
        => member is not null && _membership.TryGetValue(member, out var team) ? team : null;

    /// <summary>Collision rule for a member — <see cref="TeamCollisionRule.Always"/> when teamless, per vanilla.</summary>
    public TeamCollisionRule GetCollisionRule(string? member)
        => GetTeamOf(member)?.CollisionRule ?? TeamCollisionRule.Always;

    /// <summary>
    /// Whether two members are on the same team. Java's <c>Team.isAlliedTo</c> is reference equality on the
    /// team object, and a null team is allied to nothing.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/Team.java (isAlliedTo)
    /// </summary>
    public bool AreAllied(string? a, string? b)
    {
        var teamA = GetTeamOf(a);
        if (teamA is null) return false;
        return ReferenceEquals(teamA, GetTeamOf(b));
    }

    /// <summary>
    /// The team half of <c>EntitySelector.pushableBy</c>: whether a push between two members is allowed.
    ///
    /// Note this is symmetric in the two collision rules — every branch tests "mine OR theirs" — so it does not
    /// matter which of the pair is treated as the pusher.
    ///
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/EntitySelector.java:56-68
    /// </summary>
    public bool ArePushCompatible(string? pusher, string? pushed)
    {
        var ownRule = GetCollisionRule(pusher);
        var theirRule = GetCollisionRule(pushed);

        if (ownRule == TeamCollisionRule.Never || theirRule == TeamCollisionRule.Never) return false;

        var sameTeam = AreAllied(pusher, pushed);

        if ((ownRule == TeamCollisionRule.PushOwnTeam || theirRule == TeamCollisionRule.PushOwnTeam) && sameTeam)
        {
            return false;
        }

        if ((ownRule == TeamCollisionRule.PushOtherTeams || theirRule == TeamCollisionRule.PushOtherTeams) && !sameTeam)
        {
            return false;
        }

        return true;
    }

    public void Clear()
    {
        _teams.Clear();
        _membership.Clear();
    }
}
