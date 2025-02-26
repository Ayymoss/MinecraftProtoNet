using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class EntityEventPacket : Packet
{
    public override int PacketId => 0x1F;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement EntityEventPacket
        var entityId = buffer.ReadSignedInt();
        var type = buffer.ReadUnsignedByte();
    }
}
