using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x42, ProtocolState.Play)]
public class PlayerCombatEnterPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
