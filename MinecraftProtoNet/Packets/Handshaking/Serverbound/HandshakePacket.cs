using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Handshaking.Serverbound;

public class HandshakePacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Serverbound;
    public int ProtocolVersion { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public ushort ServerPort { get; set; }
    public int NextState { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
        buffer.WriteVarInt(ProtocolVersion);
        buffer.WriteString(ServerAddress);
        buffer.WriteUnsignedShort(ServerPort);
        buffer.WriteVarInt(NextState);
    }
}
