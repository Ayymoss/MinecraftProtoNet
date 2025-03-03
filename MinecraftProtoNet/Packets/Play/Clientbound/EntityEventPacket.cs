using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x1F, ProtocolState.Play)]
public class EntityEventPacket : IClientPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement EntityEventPacket
        var entityId = buffer.ReadSignedInt();
        var type = buffer.ReadUnsignedByte();
    }
}
