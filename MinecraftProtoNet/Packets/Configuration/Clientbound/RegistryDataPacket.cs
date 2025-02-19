using fNbt;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class RegistryDataPacket : Packet
{
    public override int PacketId => 0x07;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public string RegistryId { get; set; }
    public List<RegistryEntry> Entries { get; set; }

    
    // TODO: This packet needs to be revised. The Key is including data that it should not.
    public override void Deserialize(ref PacketBufferReader buffer)
    {
        RegistryId = buffer.ReadString();

        var count = buffer.ReadVarInt();
        Entries = new List<RegistryEntry>(count);

        for (var i = 0; i < count; i++)
        {
            Entries.Add(new RegistryEntry
            {
                Key = buffer.ReadString(),
                //Value = buffer.ReadOptionalNbtTag(readRootTag: false)
            });
        }
    }

    public class RegistryEntry
    {
        public string Key { get; set; }
        public NbtTag? Value { get; set; }
    }
}
