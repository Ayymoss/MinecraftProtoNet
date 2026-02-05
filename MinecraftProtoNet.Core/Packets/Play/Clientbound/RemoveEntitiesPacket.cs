using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x4C, ProtocolState.Play, true)]
public class RemoveEntitiesPacket : IClientboundPacket
{
    public required int[] Entities { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        Entities = new int[count];
        for (var i = 0; i < count; i++)
        {
            Entities[i] = buffer.ReadVarInt();
        }
    }
}
