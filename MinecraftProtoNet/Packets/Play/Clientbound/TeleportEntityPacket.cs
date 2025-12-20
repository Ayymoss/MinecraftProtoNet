using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent to teleport an entity to a new position.
/// </summary>
[Packet(0x7C, ProtocolState.Play)]
public class TeleportEntityPacket : IClientboundPacket
{
    /// <summary>
    /// The entity being teleported.
    /// </summary>
    public int EntityId { get; set; }
    
    /// <summary>
    /// New X position.
    /// </summary>
    public double X { get; set; }
    
    /// <summary>
    /// New Y position.
    /// </summary>
    public double Y { get; set; }
    
    /// <summary>
    /// New Z position.
    /// </summary>
    public double Z { get; set; }
    
    /// <summary>
    /// New velocity X component.
    /// </summary>
    public double VelocityX { get; set; }
    
    /// <summary>
    /// New velocity Y component.
    /// </summary>
    public double VelocityY { get; set; }
    
    /// <summary>
    /// New velocity Z component.
    /// </summary>
    public double VelocityZ { get; set; }
    
    /// <summary>
    /// Yaw rotation in degrees.
    /// </summary>
    public float Yaw { get; set; }
    
    /// <summary>
    /// Pitch rotation in degrees.
    /// </summary>
    public float Pitch { get; set; }
    
    /// <summary>
    /// Bit flags indicating which values are relative vs absolute.
    /// </summary>
    public int RelativeFlags { get; set; }
    
    /// <summary>
    /// Whether the entity is on the ground.
    /// </summary>
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        
        // PositionMoveRotation: x, y, z, velocityX, velocityY, velocityZ, yaw, pitch
        X = buffer.ReadDouble();
        Y = buffer.ReadDouble();
        Z = buffer.ReadDouble();
        VelocityX = buffer.ReadDouble();
        VelocityY = buffer.ReadDouble();
        VelocityZ = buffer.ReadDouble();
        Yaw = buffer.ReadFloat();
        Pitch = buffer.ReadFloat();
        
        // Relative flags (which values are relative vs absolute)
        RelativeFlags = buffer.ReadSignedInt();
        
        OnGround = buffer.ReadBoolean();
    }
}
