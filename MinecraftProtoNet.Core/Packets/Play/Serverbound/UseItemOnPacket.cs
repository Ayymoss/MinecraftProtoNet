using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x3F, ProtocolState.Play)]
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
