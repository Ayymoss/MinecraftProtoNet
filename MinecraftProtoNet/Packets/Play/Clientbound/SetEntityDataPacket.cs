using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;
// TODO: Partially implemented.

[Packet(0x5D, ProtocolState.Play)]
public class SetEntityDataPacket : IClientPacket
{
    public int EntityId { get; set; }
    public byte Index { get; set; }
    

    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
