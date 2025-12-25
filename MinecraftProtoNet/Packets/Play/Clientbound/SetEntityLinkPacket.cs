using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent when an entity is linked to another (e.g., leash).
/// </summary>
[Packet(0x63, ProtocolState.Play)]
public class SetEntityLinkPacket : IClientboundPacket
{
    /// <summary>
    /// The entity being attached (e.g., the mob on the leash).
    /// </summary>
    public int SourceEntityId { get; set; }
    
    /// <summary>
    /// The entity being attached to (e.g., the fence post). 0 if detaching.
    /// </summary>
    public int DestEntityId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Note: These are plain ints, not VarInts
        SourceEntityId = buffer.ReadSignedInt();
        DestEntityId = buffer.ReadSignedInt();
    }
}
