using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class UpdateTagsPacket : Packet
{
    public override int PacketId => 0x0D;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public Dictionary<string, Tag[]> Tags { get; set; } = new();

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            var registry = buffer.ReadString();
            var tagCount = buffer.ReadVarInt();
            var tags = new Tag[tagCount];
            for (var j = 0; j < tagCount; j++)
            {
                var name = buffer.ReadString();
                var entryCount = buffer.ReadVarInt();
                var entries = new int[entryCount];
                for (var k = 0; k < entryCount; k++)
                {
                    entries[k] = buffer.ReadVarInt();
                }

                tags[j] = new Tag
                {
                    Name = name,
                    Entries = entries
                };
            }

            Tags.Add(registry, tags);
        }
    }

    public class Tag
    {
        public required string Name { get; set; }
        public required int[] Entries { get; set; }
    }
}
