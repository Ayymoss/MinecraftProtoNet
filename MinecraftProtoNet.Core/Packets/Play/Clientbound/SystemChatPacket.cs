using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x78, ProtocolState.Play)]
public class SystemChatPacket : IClientboundPacket
{
    public required NbtTag Tags { get; set; }
    public bool Overlay { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Tags = buffer.ReadNbtTag();
        Overlay = buffer.ReadBoolean();
    }
}
