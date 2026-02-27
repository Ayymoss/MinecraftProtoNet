using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x0C, ProtocolState.Play)]
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
