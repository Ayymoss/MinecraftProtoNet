using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Handshaking.Serverbound;

[Packet(0x00, ProtocolState.Handshaking)]
public class HandshakePacket : IServerboundPacket
{
    public int ProtocolVersion { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public ushort ServerPort { get; set; }
    public int NextState { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(ProtocolVersion);
        buffer.WriteString(ServerAddress);
        buffer.WriteUnsignedShort(ServerPort);
        buffer.WriteVarInt(NextState);
    }
}
