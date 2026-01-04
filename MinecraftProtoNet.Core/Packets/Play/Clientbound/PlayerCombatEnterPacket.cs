using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x42, ProtocolState.Play)]
public class PlayerCombatEnterPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
