using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

[Packet(0x0E, ProtocolState.Configuration)]
public class SelectKnownPacksPacket : IClientboundPacket
{
    public required Packs[] KnownPacks { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        var packs = new Packs[count];
        for (var i = 0; i < count; i++)
        {
            var pack = new Packs
            {
                Namespace = buffer.ReadString(),
                Id = buffer.ReadString(),
                Version = buffer.ReadString()
            };
            packs[i] = pack;
        }

        KnownPacks = packs;
    }

    public sealed class Packs
    {
        public required string Namespace { get; set; }
        public required string Id { get; set; }
        public required string Version { get; set; }
    }
}
