using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent when the player respawns or changes dimension.
/// Contains the same spawn info as LoginPacket.
/// </summary>
[Packet(0x51, ProtocolState.Play)]
public class RespawnPacket : IClientboundPacket
{
    public const byte KeepAttributeModifiers = 1;
    public const byte KeepEntityData = 2;
    public const byte KeepAllData = 3;
    
    // CommonPlayerSpawnInfo fields
    public int DimensionType { get; set; }
    public string DimensionName { get; set; } = string.Empty;
    public long HashedSeed { get; set; }
    public byte GameMode { get; set; }
    public sbyte PreviousGameMode { get; set; }
    public bool IsDebug { get; set; }
    public bool IsFlat { get; set; }
    public bool HasDeathLocation { get; set; }
    public string? DeathDimensionName { get; set; }
    public Vector3<double>? DeathLocation { get; set; }
    public int PortalCooldown { get; set; }
    public int SeaLevel { get; set; }
    
    /// <summary>
    /// Bit mask for what data to keep from the previous player entity.
    /// </summary>
    public byte DataToKeep { get; set; }
    
    /// <summary>
    /// Whether to keep attribute modifiers from the previous player entity.
    /// </summary>
    public bool ShouldKeepAttributeModifiers => (DataToKeep & KeepAttributeModifiers) != 0;
    
    /// <summary>
    /// Whether to keep entity data from the previous player entity.
    /// </summary>
    public bool ShouldKeepEntityData => (DataToKeep & KeepEntityData) != 0;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // CommonPlayerSpawnInfo
        DimensionType = buffer.ReadVarInt();
        DimensionName = buffer.ReadString();
        HashedSeed = buffer.ReadSignedLong();
        GameMode = buffer.ReadUnsignedByte();
        PreviousGameMode = buffer.ReadSignedByte();
        IsDebug = buffer.ReadBoolean();
        IsFlat = buffer.ReadBoolean();
        HasDeathLocation = buffer.ReadBoolean();
        DeathDimensionName = HasDeathLocation ? buffer.ReadString() : null;
        DeathLocation = HasDeathLocation ? buffer.ReadCoordinatePosition() : null;
        PortalCooldown = buffer.ReadVarInt();
        SeaLevel = buffer.ReadVarInt();
        
        // RespawnPacket-specific field
        DataToKeep = buffer.ReadUnsignedByte();
    }
}
