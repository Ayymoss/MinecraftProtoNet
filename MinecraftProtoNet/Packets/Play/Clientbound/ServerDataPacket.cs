using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x4F, ProtocolState.Play)]
public class ServerDataPacket : IClientboundPacket
{
    public NbtTag Motd { get; set; }
    public byte[] Icon { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Motd = buffer.ReadNbtTag();
        Icon = buffer.ReadPrefixedArray<byte>();
    }
}
