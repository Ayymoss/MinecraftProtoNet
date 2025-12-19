using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x6E, ProtocolState.Play)]
public class SetSimulationDistancePacket : IClientboundPacket
{
    public int SimulationDistance { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        SimulationDistance = buffer.ReadVarInt();
    }
}
