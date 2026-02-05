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
        // Read optional entity IDs: Java uses readOptionalEntityId which returns varInt - 1
        // So 0 means -1 (no entity), 1 means entity ID 0, etc.
        SourceCauseId = buffer.ReadVarInt() - 1;
        SourceDirectId = buffer.ReadVarInt() - 1;

        if (!buffer.ReadBoolean()) return;

        var x = buffer.ReadDouble();
        var y = buffer.ReadDouble();
        var z = buffer.ReadDouble();
        SourcePosition = new Vector3<double>(x, y, z);
    }
}
