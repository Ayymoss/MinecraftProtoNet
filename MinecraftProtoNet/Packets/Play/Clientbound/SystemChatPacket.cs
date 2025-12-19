using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x71, ProtocolState.Play)]
public class SystemChatPacket : IClientboundPacket
{
    public NbtTag Tags { get; set; }
    public bool Overlay { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Tags = buffer.ReadNbtTag();
        Overlay = buffer.ReadBoolean();
    }
}
