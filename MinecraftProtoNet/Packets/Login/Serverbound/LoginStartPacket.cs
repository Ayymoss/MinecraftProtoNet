using System.ComponentModel.DataAnnotations;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

[Packet(0x00, ProtocolState.Login)]
public class LoginStartPacket : IServerPacket
{
    [Length(3, 16)] public required string Username { get; set; } = string.Empty;
    public Guid? Uuid { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));

        buffer.WriteString(Username);

        if (Uuid.HasValue)
        {
            buffer.WriteUUID(Uuid.Value);
        }
        else
        {
            buffer.WriteUnsignedByte(0);
        }
    }
}
