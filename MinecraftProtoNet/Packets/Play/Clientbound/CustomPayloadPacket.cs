using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x18, ProtocolState.Play)]
public class CustomPayloadPacket : IClientboundPacket
{
    public string Channel { get; set; }
    public byte[] Data { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Channel = buffer.ReadString();
        Data = buffer.ReadRestBuffer().ToArray();
    }
}
