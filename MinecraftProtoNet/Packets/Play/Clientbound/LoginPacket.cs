using System.Numerics;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class LoginPacket : Packet
{
    public override int PacketId => 0x2C;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public LoginPayload Login { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        var entityId = buffer.ReadSignedInt();
        var isHardcore = buffer.ReadBoolean();
        var dimensionNames = buffer.ReadPrefixedArray<string>();
        var maxPlayers = buffer.ReadVarInt();
        var viewDistance = buffer.ReadVarInt();
        var simulationDistance = buffer.ReadVarInt();
        var reducedDebugInfo = buffer.ReadBoolean();
        var enableRespawnScreen = buffer.ReadBoolean();
        var doLimitedCrafting = buffer.ReadBoolean();
        var dimensionType = buffer.ReadVarInt();
        var dimensionName = buffer.ReadString();
        var hashedSeed = buffer.ReadSignedLong();
        var gameMode = buffer.ReadUnsignedByte();
        var previousGameMode = buffer.ReadSignedByte();
        var isDebug = buffer.ReadBoolean();
        var isFlat = buffer.ReadBoolean();
        var hasDeathLocation = buffer.ReadBoolean();
        var deathDimensionName = hasDeathLocation ? buffer.ReadString() : null;
        var deathLocation = hasDeathLocation ? buffer.ReadPosition() : new Vector3?();
        var portalCooldown = buffer.ReadVarInt();
        var seaLevel = buffer.ReadVarInt();
        var enforcesSecureChat = buffer.ReadBoolean();

        Login = new LoginPayload
        {
            EntityId = entityId,
            IsHardcore = isHardcore,
            DimensionNames = dimensionNames,
            MaxPlayers = maxPlayers,
            ViewDistance = viewDistance,
            SimulationDistance = simulationDistance,
            ReducedDebugInfo = reducedDebugInfo,
            EnableRespawnScreen = enableRespawnScreen,
            DoLimitedCrafting = doLimitedCrafting,
            DimensionType = dimensionType,
            DimensionName = dimensionName,
            HashedSeed = hashedSeed,
            GameMode = gameMode,
            PreviousGameMode = previousGameMode,
            IsDebug = isDebug,
            IsFlat = isFlat,
            HasDeathLocation = hasDeathLocation,
            DeathDimensionName = deathDimensionName,
            DeathLocation = deathLocation,
            PortalCooldown = portalCooldown,
            SeaLevel = seaLevel,
            EnforcesSecureChat = enforcesSecureChat
        };
    }

    public class LoginPayload
    {
        public required int EntityId { get; set; }
        public required bool IsHardcore { get; set; }
        public required string[] DimensionNames { get; set; }
        public required int MaxPlayers { get; set; }
        public required int ViewDistance { get; set; }
        public required int SimulationDistance { get; set; }
        public required bool ReducedDebugInfo { get; set; }
        public required bool EnableRespawnScreen { get; set; }
        public required bool DoLimitedCrafting { get; set; }
        public required int DimensionType { get; set; }
        public required string DimensionName { get; set; }
        public required long HashedSeed { get; set; }
        public required byte GameMode { get; set; }
        public required sbyte PreviousGameMode { get; set; }
        public required bool IsDebug { get; set; }
        public required bool IsFlat { get; set; }
        public required bool HasDeathLocation { get; set; }
        public required string? DeathDimensionName { get; set; }
        public required Vector3? DeathLocation { get; set; }
        public required int PortalCooldown { get; set; }
        public required int SeaLevel { get; set; }
        public required bool EnforcesSecureChat { get; set; }
    }
}
