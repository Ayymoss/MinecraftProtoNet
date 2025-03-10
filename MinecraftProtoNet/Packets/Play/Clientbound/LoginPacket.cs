using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x2C, ProtocolState.Play)]
public class LoginPacket : IClientPacket
{
    public int EntityId { get; set; }
    public bool IsHardcore { get; set; }
    public string[] DimensionNames { get; set; } = [];
    public int MaxPlayers { get; set; }
    public int ViewDistance { get; set; }
    public int SimulationDistance { get; set; }
    public bool ReducedDebugInfo { get; set; }
    public bool EnableRespawnScreen { get; set; }
    public bool DoLimitedCrafting { get; set; }
    public int DimensionType { get; set; }
    public string DimensionName { get; set; } = string.Empty;
    public long HashedSeed { get; set; }
    public byte GameMode { get; set; }
    public sbyte PreviousGameMode { get; set; }
    public bool IsDebug { get; set; }
    public bool IsFlat { get; set; }
    public bool HasDeathLocation { get; set; }
    public string? DeathDimensionName { get; set; }
    public Vector3? DeathLocation { get; set; }
    public int PortalCooldown { get; set; }
    public int SeaLevel { get; set; }
    public bool EnforcesSecureChat { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadSignedInt();
        IsHardcore = buffer.ReadBoolean();
        DimensionNames = buffer.ReadPrefixedArray<string>();
        MaxPlayers = buffer.ReadVarInt();
        ViewDistance = buffer.ReadVarInt();
        SimulationDistance = buffer.ReadVarInt();
        ReducedDebugInfo = buffer.ReadBoolean();
        EnableRespawnScreen = buffer.ReadBoolean();
        DoLimitedCrafting = buffer.ReadBoolean();
        DimensionType = buffer.ReadVarInt();
        DimensionName = buffer.ReadString();
        HashedSeed = buffer.ReadSignedLong();
        GameMode = buffer.ReadUnsignedByte();
        PreviousGameMode = buffer.ReadSignedByte();
        IsDebug = buffer.ReadBoolean();
        IsFlat = buffer.ReadBoolean();
        HasDeathLocation = buffer.ReadBoolean();
        DeathDimensionName = HasDeathLocation ? buffer.ReadString() : null;
        DeathLocation = HasDeathLocation ? buffer.ReadAsPosition() : null;
        PortalCooldown = buffer.ReadVarInt();
        SeaLevel = buffer.ReadVarInt();
        EnforcesSecureChat = buffer.ReadBoolean();
    }
}
