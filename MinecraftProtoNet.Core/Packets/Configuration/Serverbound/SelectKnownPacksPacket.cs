using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

[Packet(0x07, ProtocolState.Configuration)]
public class SelectKnownPacksPacket : IServerboundPacket
{
    public required Packs[] KnownPacks { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
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
        public required string Namespace { get; set; }
        public required string Id { get; set; }
        public required string Version { get; set; }
    }
}
