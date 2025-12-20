using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent when a mob effect is applied or updated on an entity.
/// </summary>
[Packet(0x83, ProtocolState.Play)]
public class UpdateMobEffectPacket : IClientboundPacket
{
    private const byte FlagAmbient = 1;
    private const byte FlagVisible = 2;
    private const byte FlagShowIcon = 4;
    private const byte FlagBlend = 8;
    
    /// <summary>
    /// The entity the effect is applied to.
    /// </summary>
    public int EntityId { get; set; }
    
    /// <summary>
    /// The effect ID from the registry (VarInt holder reference).
    /// </summary>
    public int EffectId { get; set; }
    
    /// <summary>
    /// Effect amplifier (0 = level 1, 1 = level 2, etc.).
    /// </summary>
    public int Amplifier { get; set; }
    
    /// <summary>
    /// Duration in ticks. -1 for infinite.
    /// </summary>
    public int DurationTicks { get; set; }
    
    /// <summary>
    /// Packed flags byte.
    /// </summary>
    public byte Flags { get; set; }
    
    /// <summary>
    /// Whether the effect was applied by a beacon or conduit.
    /// </summary>
    public bool IsAmbient => (Flags & FlagAmbient) != 0;
    
    /// <summary>
    /// Whether the effect particles are visible.
    /// </summary>
    public bool IsVisible => (Flags & FlagVisible) != 0;
    
    /// <summary>
    /// Whether the effect icon is shown in the inventory.
    /// </summary>
    public bool ShowIcon => (Flags & FlagShowIcon) != 0;
    
    /// <summary>
    /// Whether to blend the effect (smooth transition).
    /// </summary>
    public bool ShouldBlend => (Flags & FlagBlend) != 0;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        EffectId = buffer.ReadVarInt();
        Amplifier = buffer.ReadVarInt();
        DurationTicks = buffer.ReadVarInt();
        Flags = buffer.ReadUnsignedByte();
    }
}
