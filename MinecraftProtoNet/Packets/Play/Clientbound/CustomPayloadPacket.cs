using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x18, ProtocolState.Play)]
public class CustomPayloadPacket : IClientboundPacket
{
    public string Channel { get; set; } = default!;
    public byte[] Data { get; set; } = default!;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Channel = buffer.ReadString();
        Data = buffer.ReadRestBuffer().ToArray();
    }
}
