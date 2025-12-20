using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x0B, ProtocolState.Play)]
public class ClientCommandPacket : IServerboundPacket
{
    public required Action ActionId { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt((int)ActionId);
    }

    public enum Action
    {
        PerformRespawn,
        RequestStats
    }
}
