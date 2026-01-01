using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x4C, ProtocolState.Play, true)]
public class RemoveEntitiesPacket : IClientboundPacket
{
    public int[] Entities { get; set; }

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
