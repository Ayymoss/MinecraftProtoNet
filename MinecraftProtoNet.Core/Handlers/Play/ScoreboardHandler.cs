using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Maintains the client-side mirror of the server scoreboard's teams.
///
/// The bot has no use for team colours or prefixes; it tracks teams because vanilla gates entity-to-entity
/// pushing on each team's collision rule, and lobby servers rely on that to keep crowds from shoving players
/// around. See <see cref="MinecraftProtoNet.Core.State.TeamRegistry"/>.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientPacketListener.java:2058-2096
/// </summary>
[HandlesPacket(typeof(SetPlayerTeamPacket))]
public class ScoreboardHandler(ILogger<ScoreboardHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ScoreboardHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        if (packet is not SetPlayerTeamPacket teamPacket) return Task.CompletedTask;

        var teams = client.State.Level.Teams;

        // Java looks the team up (warning and bailing when unknown) rather than creating it for non-ADD
        // methods; GetOrCreate is equivalent here because an unknown team has no members and default rules,
        // so a stray CHANGE/JOIN cannot make us push differently than vanilla would.
        if (teamPacket.CollisionRule is { } collisionRule)
        {
            var team = teams.GetOrCreate(teamPacket.Name);
            team.CollisionRule = collisionRule;
            if (teamPacket.NameTagVisibility is { } visibility) team.NameTagVisibility = visibility;

            logger.LogDebug("Team {Team}: collisionRule={Rule}, nameTagVisibility={Visibility}",
                teamPacket.Name, collisionRule, team.NameTagVisibility);
        }

        switch (teamPacket.Method)
        {
            case SetPlayerTeamPacket.TeamMethod.Add:
            case SetPlayerTeamPacket.TeamMethod.Join:
                teams.AddMembers(teamPacket.Name, teamPacket.Members);
                break;

            case SetPlayerTeamPacket.TeamMethod.Leave:
                teams.RemoveMembers(teamPacket.Name, teamPacket.Members);
                break;

            case SetPlayerTeamPacket.TeamMethod.Remove:
                teams.Remove(teamPacket.Name);
                break;
        }

        return Task.CompletedTask;
    }
}
