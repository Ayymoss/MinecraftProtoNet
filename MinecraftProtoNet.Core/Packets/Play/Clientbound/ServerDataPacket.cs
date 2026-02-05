using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x55, ProtocolState.Play)]
public class ServerDataPacket : IClientboundPacket
{
    public required NbtTag Motd { get; set; }
    public required byte[] Icon { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Motd = buffer.ReadNbtTag();
        Icon = buffer.ReadPrefixedArray<byte>();
    }
}
