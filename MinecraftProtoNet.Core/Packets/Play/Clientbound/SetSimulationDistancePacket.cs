using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x6E, ProtocolState.Play)]
public class SetSimulationDistancePacket : IClientboundPacket
{
    public int SimulationDistance { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        SimulationDistance = buffer.ReadVarInt();
    }
}
