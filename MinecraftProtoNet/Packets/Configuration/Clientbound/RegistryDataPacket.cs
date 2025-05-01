using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

[Packet(0x07, ProtocolState.Configuration)]
public class RegistryDataPacket : IClientboundPacket
{
    public string RegistryId { get; set; }
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
