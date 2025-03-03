using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x1D, ProtocolState.Play)]
public class DisconnectPacket : IClientPacket
{
    public string DisconnectReason { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        DisconnectReason = buffer.ReadString();
    }
}
