using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Serverbound;

[Packet(0x07, ProtocolState.Configuration)]
public class SelectKnownPacksPacket : IServerPacket
{
    public required Packs[] KnownPacks { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketId());

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
