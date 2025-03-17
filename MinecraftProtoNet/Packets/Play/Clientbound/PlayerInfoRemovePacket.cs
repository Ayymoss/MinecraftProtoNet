using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x3F, ProtocolState.Play)]
public class PlayerInfoRemovePacket : IClientPacket
{
    public Guid[] Uuids { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Uuids = buffer.ReadPrefixedArray<Guid>();
    }
}
