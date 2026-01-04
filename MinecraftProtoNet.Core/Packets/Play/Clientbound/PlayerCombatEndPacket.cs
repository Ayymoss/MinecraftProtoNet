using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x41, ProtocolState.Play)]
public class PlayerCombatEndPacket : IClientboundPacket
{
    public int DurationInTicks { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        DurationInTicks = buffer.ReadVarInt();
    }
}
