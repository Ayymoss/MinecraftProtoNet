using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class TickingStepPacket : Packet
{
    public override int PacketId => 0x79;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public TickingStep Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new TickingStep
        {
            TickSteps = buffer.ReadVarInt(),
        };
    }

    public class TickingStep
    {
        public required int TickSteps { get; set; }
    }
}
