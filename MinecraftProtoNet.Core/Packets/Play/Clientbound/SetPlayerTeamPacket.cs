using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Creates, removes, or updates a scoreboard team.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetPlayerTeamPacket.java:49-64
/// </summary>
[Packet(0x6D, ProtocolState.Play, silent: true)]
public class SetPlayerTeamPacket : IClientboundPacket
{
    public enum TeamMethod
    {
        Add = 0,
        Remove = 1,
        Change = 2,
        Join = 3,
        Leave = 4
    }

    public string Name { get; private set; } = string.Empty;
    public TeamMethod Method { get; private set; }

    /// <summary>Present only for <see cref="TeamMethod.Add"/> and <see cref="TeamMethod.Change"/>.</summary>
    public TeamCollisionRule? CollisionRule { get; private set; }

    /// <summary>Present only for <see cref="TeamMethod.Add"/> and <see cref="TeamMethod.Change"/>.</summary>
    public TeamNameTagVisibility? NameTagVisibility { get; private set; }

    /// <summary>Present only for <see cref="TeamMethod.Add"/>, <see cref="TeamMethod.Join"/>, <see cref="TeamMethod.Leave"/>.</summary>
    public string[] Members { get; private set; } = [];

    /// <summary>Text shown before a member's name. Carries the sidebar line text on SkyBlock.</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>Text shown after a member's name. Carries the rest of the sidebar line on SkyBlock.</summary>
    public string Suffix { get; private set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Name = buffer.ReadString();
        Method = (TeamMethod)buffer.ReadUnsignedByte();

        // shouldHaveParameters: method == 0 || method == 2
        if (Method is TeamMethod.Add or TeamMethod.Change)
        {
            // Parameters record order (26.2): displayName, playerPrefix, playerSuffix, nameTagVisibility,
            // collisionRule, Optional<TeamColor>, options byte.
            // NOTE: this order changed from earlier versions, where the options byte came second and
            // visibility/collisionRule were strings.
            // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetPlayerTeamPacket.java:143-152
            buffer.ReadChatComponent(); // displayName

            // Kept, not skipped: SkyBlock renders its sidebar by giving each line an invisible score holder
            // and putting the visible text in that holder's team prefix and suffix. Discarding these leaves
            // the client unable to read anything on the scoreboard — the purse included.
            Prefix = buffer.ReadChatComponent();
            Suffix = buffer.ReadChatComponent();

            // Visibility / CollisionRule / TeamColor are all ByteBufCodecs.idMapper, i.e. VarInt.
            // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java:538-543
            NameTagVisibility = (TeamNameTagVisibility)buffer.ReadVarInt();
            CollisionRule = (TeamCollisionRule)buffer.ReadVarInt();

            if (buffer.ReadBoolean()) buffer.ReadVarInt(); // Optional<TeamColor>
            buffer.ReadUnsignedByte(); // packed option flags (friendly fire, see invisibles)
        }

        // shouldHavePlayerList: method == 0 || method == 3 || method == 4
        if (Method is TeamMethod.Add or TeamMethod.Join or TeamMethod.Leave)
        {
            var count = buffer.ReadVarInt();
            var members = new string[count];
            for (var i = 0; i < count; i++)
            {
                members[i] = buffer.ReadString();
            }

            Members = members;
        }
    }
}
