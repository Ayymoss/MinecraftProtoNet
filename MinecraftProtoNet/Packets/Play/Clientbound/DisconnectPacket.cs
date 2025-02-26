using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class DisconnectPacket : Packet
{
    public override int PacketId => 0x1D;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public Disconnect Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new Disconnect { DisconnectReason = buffer.ReadString() };
    }

    public class Disconnect
    {
        public required string DisconnectReason { get; set; }
    }
}
