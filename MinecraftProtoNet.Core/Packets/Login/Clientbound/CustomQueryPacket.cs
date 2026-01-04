using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

[Packet(0x04, ProtocolState.Login)]
public class CustomQueryPacket : IClientboundPacket
{
    public int MessageId { get; set; }
    public required string Channel { get; set; }
    public required byte[] Data { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        MessageId = buffer.ReadVarInt();
        Channel = buffer.ReadString();
        Data = buffer.ReadRestBuffer().ToArray();
    }
}
