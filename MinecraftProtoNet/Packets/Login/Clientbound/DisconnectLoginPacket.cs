using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

public class DisconnectLoginPacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public string Reason { get; set; } = string.Empty; // JSON formatted

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Reason = buffer.ReadString();
    }
}
