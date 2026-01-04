using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

[Packet(0x07, ProtocolState.Configuration)]
public class RegistryDataPacket : IClientboundPacket
{
    public required string RegistryId { get; set; }
    public Dictionary<string, NbtTag?> Tags { get; set; } = new();

    public void Deserialize(ref PacketBufferReader buffer)
    {
        RegistryId = buffer.ReadString();
        var count = buffer.ReadVarInt();

        for (var i = 0; i < count; i++)
        {
            var key = buffer.ReadString();
            var tag = buffer.ReadOptionalNbtTag();
            Tags.Add(key, tag);
        }
    }
}
