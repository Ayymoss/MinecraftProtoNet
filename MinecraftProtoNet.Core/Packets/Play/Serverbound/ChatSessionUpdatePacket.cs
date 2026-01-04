using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x09, ProtocolState.Play)]
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
