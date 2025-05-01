using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x19, ProtocolState.Play)]
public class DamageEventPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public int SourceTypeId { get; set; }
    public int SourceCauseId { get; set; }
    public int SourceDirectId { get; set; }
    public Vector3<double>? SourcePosition { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        SourceTypeId = buffer.ReadVarInt();
        SourceCauseId = buffer.ReadVarInt();
        SourceDirectId = buffer.ReadVarInt();

        if (!buffer.ReadBoolean()) return;

        var x = buffer.ReadDouble();
        var y = buffer.ReadDouble();
        var z = buffer.ReadDouble();
        SourcePosition = new Vector3<double>(x, y, z);
    }
}
