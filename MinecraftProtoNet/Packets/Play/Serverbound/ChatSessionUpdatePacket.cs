using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x08, ProtocolState.Play)]
public class ChatSessionUpdatePacket : IServerboundPacket
{
    public required Guid SessionId { get; set; }
    public required long ExpiresAt { get; set; }
    public required byte[] PublicKey { get; set; }
    public required byte[] KeySignature { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUUID(SessionId);
        buffer.WriteSignedLong(ExpiresAt);
        buffer.WriteVarInt(PublicKey.Length);
        buffer.WriteBuffer(PublicKey);
        buffer.WriteVarInt(KeySignature.Length);
        buffer.WriteBuffer(KeySignature);
    }
}
