using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x41, ProtocolState.Play)]
public class PlayerCombatEndPacket : IClientboundPacket
{
    public int DurationInTicks { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        DurationInTicks = buffer.ReadVarInt();
    }
}
