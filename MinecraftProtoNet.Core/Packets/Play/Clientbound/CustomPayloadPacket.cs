using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
