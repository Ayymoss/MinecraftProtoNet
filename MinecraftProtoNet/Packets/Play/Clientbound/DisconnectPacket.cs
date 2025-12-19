using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.NBT.Tags.Abstract;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x20, ProtocolState.Play)]
public class DisconnectPacket : IClientboundPacket
{
    public NbtTag DisconnectReason { get; set; } = new NbtEnd();

    public void Deserialize(ref PacketBufferReader buffer)
    {
        DisconnectReason = buffer.ReadNbtTag();
    }
}
