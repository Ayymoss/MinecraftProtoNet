using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
