using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

public class ClientCommandPacket : Packet
{
    public override int PacketId => 0x0A;
    public override PacketDirection Direction => PacketDirection.Serverbound;

    public required Action ActionId { get; set; }

    public override void Serialize(ref PacketBufferWriter buffer)
    {
        base.Serialize(ref buffer);

        buffer.WriteVarInt((int)ActionId);
    }

    public enum Action
    {
        PerformRespawn,
        RequestStats
    }
}
