using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

[Packet(0x01, ProtocolState.Login)]
public class HelloPacket : IClientboundPacket
{
    public required string ServerId { get; set; }
    public required byte[] PublicKey { get; set; }
    public required byte[] VerifyToken { get; set; }
    public bool ShouldAuthenticate { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ServerId = buffer.ReadString();
        PublicKey = buffer.ReadPrefixedArray<byte>();
        VerifyToken = buffer.ReadPrefixedArray<byte>();
        ShouldAuthenticate = buffer.ReadBoolean();
    }
}
