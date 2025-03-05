using System.ComponentModel.DataAnnotations;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x07, ProtocolState.Play)]
public class ChatPacket : IServerPacket
{
    public ChatPacket(string message)
    {
        Message = message;
        Timestamp = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds();
        Salt = 0;
        Signature = null;
        MessageCount = 0;
        Acknowledged = [0, 0, 0];
    }

    [MaxLength(256)] public string Message { get; set; }
    public long Timestamp { get; set; }
    public long Salt { get; set; }

    /// <summary>
    /// This needs to be 256 bytes long
    /// </summary>
    [MinLength(256)]
    public byte[]? Signature { get; set; }

    public int MessageCount { get; set; }
    public byte[] Acknowledged { get; set; } = [0, 0, 0];

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));

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
    }
}
