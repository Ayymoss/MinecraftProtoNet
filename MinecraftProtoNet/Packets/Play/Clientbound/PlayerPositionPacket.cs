using System.Numerics;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class PlayerPositionPacket : Packet
{
    public override int PacketId => 0x42;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public int TeleportId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector2 Rotation { get; set; }
    public PositionFlags Flags { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        TeleportId = buffer.ReadVarInt();

        var x = buffer.ReadDouble();
        var y = buffer.ReadDouble();
        var z = buffer.ReadDouble();
        Position = new Vector3((float)x, (float)y, (float)z);

        var velX = buffer.ReadDouble();
        var velY = buffer.ReadDouble();
        var velZ = buffer.ReadDouble();
        Velocity = new Vector3((float)velX, (float)velY, (float)velZ);

        var yaw = buffer.ReadFloat();
        var pitch = buffer.ReadFloat();
        Rotation = new Vector2(yaw, pitch);

        Flags = (PositionFlags)buffer.ReadUnsignedInt();
    }

    public enum PositionFlags : sbyte
    {
        None,
        X,
        Y,
        Z,
        RotationY,
        RotationX,
        DeltaX,
        DeltaY,
        DeltaZ,
        RotateDelta
    }
}
