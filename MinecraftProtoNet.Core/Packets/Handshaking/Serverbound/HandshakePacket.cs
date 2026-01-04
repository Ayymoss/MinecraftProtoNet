using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Handshaking.Serverbound;

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
