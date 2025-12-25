using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent when a mob effect is removed from an entity.
/// </summary>
[Packet(0x4D, ProtocolState.Play)]
public class RemoveMobEffectPacket : IClientboundPacket
{
    /// <summary>
    /// The entity the effect was removed from.
    /// </summary>
    public int EntityId { get; set; }
    
    /// <summary>
    /// The effect ID from the registry (VarInt holder reference).
    /// </summary>
    public int EffectId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        EffectId = buffer.ReadVarInt();
    }
}
