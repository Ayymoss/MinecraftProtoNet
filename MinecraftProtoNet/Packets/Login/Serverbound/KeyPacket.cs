using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

[Packet(0x01, ProtocolState.Login)]
public class KeyPacket : IServerboundPacket
{
    public required byte[] SharedSecret { get; set; }
    public required byte[] VerifyToken { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(SharedSecret.Length);
        buffer.WriteBuffer(SharedSecret);

        buffer.WriteVarInt(VerifyToken.Length);
        buffer.WriteBuffer(VerifyToken);
    }
}
