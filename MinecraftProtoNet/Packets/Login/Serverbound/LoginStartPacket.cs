using System.ComponentModel.DataAnnotations;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

public class LoginStartPacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Serverbound;
    [Length(3, 16)] public required string Username { get; set; } = string.Empty;
    public Guid? Uuid { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(PacketId);
        buffer.WriteString(Username);

        if (Uuid.HasValue)
        {
            buffer.WriteUUID(Uuid.Value);
        }
        else
        {
            buffer.WriteSignedByte(0);
        }
    }
}
