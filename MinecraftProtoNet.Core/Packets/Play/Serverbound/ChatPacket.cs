using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x08, ProtocolState.Play)]
public class ChatPacket : IServerboundPacket
{
    [SetsRequiredMembers]
     public ChatPacket(string message)
    {
        Message = message;
        Timestamp = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds();
        Salt = 0;
        Signature = null;
        MessageCount = 0;
        Acknowledged = [0, 0, 0];
    }

    public ChatPacket()
    {
    }

    [MaxLength(256)] public required string Message { get; set; }
    public long Timestamp { get; set; }
    public long Salt { get; set; }

    /// <summary>
    /// This needs to be 256 bytes long
    /// </summary>
    [MinLength(256)]
    public byte[]? Signature { get; set; }

    public int MessageCount { get; set; }
    public byte[] Acknowledged { get; set; } = [0, 0, 0];
    public byte Checksum { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Message);
        buffer.WriteSignedLong(Timestamp);
        buffer.WriteSignedLong(Salt);

        if (Signature is not null)
        {
            buffer.WriteBoolean(true);
            buffer.WriteBuffer(Signature);
        }
        else
        {
            buffer.WriteBoolean(false);
        }

        buffer.WriteVarInt(MessageCount);
        buffer.WriteBuffer(Acknowledged);
        buffer.WriteUnsignedByte(Checksum);
    }
}
