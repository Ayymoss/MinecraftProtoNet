using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

public class SelectKnownPacksPacket : Packet
{
    public override int PacketId => 0x07;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public required Packs[] KnownPacks { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        base.Serialize(ref buffer);

        buffer.WriteVarInt(KnownPacks.Length);
        foreach (var pack in KnownPacks)
        {
            buffer.WriteString(pack.Namespace);
            buffer.WriteString(pack.Id);
            buffer.WriteString(pack.Version);
        }
    }

    public sealed class Packs
    {
        public string Namespace { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
    }
}
