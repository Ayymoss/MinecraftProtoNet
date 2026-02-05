using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x23, ProtocolState.Play, true)]
public class EntityPositionSyncPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public required Vector3<double> Position { get; set; }
    public required Vector3<double> Velocity { get; set; }
    public required Vector2<float> YawPitch { get; set; }
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
        YawPitch = new Vector2<float>(yaw, pitch);

        OnGround = buffer.ReadBoolean();
    }
}
