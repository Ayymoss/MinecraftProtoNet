using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

public class AcceptTeleportationPacket : Packet
{
    public override int PacketId => 0x00;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public required int TeleportId { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        base.Serialize(ref buffer);

        buffer.WriteVarInt(TeleportId);
    }
}
