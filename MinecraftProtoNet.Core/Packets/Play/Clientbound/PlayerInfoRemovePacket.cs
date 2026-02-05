using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x44, ProtocolState.Play)]
public class PlayerInfoRemovePacket : IClientboundPacket
{
    public required Guid[] Uuids { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Uuids = buffer.ReadPrefixedArray<Guid>();
    }
}
