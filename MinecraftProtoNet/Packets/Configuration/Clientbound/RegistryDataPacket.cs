using System.Text;
using System.Text.RegularExpressions;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class RegistryDataPacket : Packet
{
    public override int PacketId => 0x07;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public string RegistryId { get; set; }

    public Dictionary<string, NbtTag?> Tags { get; set; } = new();

    public override void Deserialize(ref PacketBufferReader buffer)
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
