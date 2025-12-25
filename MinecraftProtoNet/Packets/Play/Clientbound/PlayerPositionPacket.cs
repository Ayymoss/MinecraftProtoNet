using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x47, ProtocolState.Play)]
public class PlayerPositionPacket : IClientboundPacket
{
    public int TeleportId { get; set; }
    public Vector3<double> Position { get; set; }
    public Vector3<double> Velocity { get; set; }
    public Vector2<float> YawPitch { get; set; }
    public PositionFlags Flags { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        TeleportId = buffer.ReadVarInt();

        var x = buffer.ReadDouble();
        var y = buffer.ReadDouble();
        var z = buffer.ReadDouble();
        Position = new Vector3<double>(x, y, z);

        var velX = buffer.ReadDouble();
        var velY = buffer.ReadDouble();
        var velZ = buffer.ReadDouble();
        Velocity = new Vector3<double>(velX, velY, velZ);

        var yaw = buffer.ReadFloat();
        var pitch = buffer.ReadFloat();
        YawPitch = new Vector2<float>(yaw, pitch);

        Flags = (PositionFlags)buffer.ReadUnsignedInt();
    }

    [Flags]
    public enum PositionFlags : int
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        Y_ROT = 1 << 3,
        X_ROT = 1 << 4,
        Delta_X = 1 << 5,
        Delta_Y = 1 << 6,
        Delta_Z = 1 << 7,
        Rotate_Delta = 1 << 8
    }
}
