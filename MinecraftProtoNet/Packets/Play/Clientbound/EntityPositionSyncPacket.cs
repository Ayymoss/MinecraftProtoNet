using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x20, ProtocolState.Play, true)]
public class EntityPositionSyncPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; }
    public Vector3<double> Velocity { get; set; }
    public Vector2D YawPitch { get; set; }
    public bool OnGround { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();

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
        YawPitch = new Vector2D(yaw, pitch);

        OnGround = buffer.ReadBoolean();
    }
}
