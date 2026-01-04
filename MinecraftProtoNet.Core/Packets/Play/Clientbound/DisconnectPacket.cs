using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x20, ProtocolState.Play)]
public class DisconnectPacket : IClientboundPacket
{
    public NbtTag DisconnectReason { get; set; } = new NbtEnd();

    public void Deserialize(ref PacketBufferReader buffer)
    {
        DisconnectReason = buffer.ReadNbtTag();
    }
}
