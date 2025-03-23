using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x3D, ProtocolState.Play)]
public class PlayerCombatEnterPacket : IClientPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
    }
}
