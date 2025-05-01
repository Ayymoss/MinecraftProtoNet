using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x3C, ProtocolState.Play)]
public class UseItemOnPacket : IServerboundPacket
{
    public required Hand Hand { get; set; }
    public required Vector3<double> Position { get; set; }
    public required BlockFace BlockFace { get; set; }
    public required Vector3<float> Cursor { get; set; }
    public required bool InsideBlock { get; set; }
    public bool WorldBorderHit { get; set; } = false;
    public required int Sequence { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt((int)Hand);
        buffer.WritePosition(Position);
        buffer.WriteVarInt((int)BlockFace);
        buffer.WriteFloat(Cursor.X);
        buffer.WriteFloat(Cursor.Y);
        buffer.WriteFloat(Cursor.Z);
        buffer.WriteBoolean(InsideBlock);
        buffer.WriteBoolean(WorldBorderHit);
        buffer.WriteVarInt(Sequence);
    }
}
