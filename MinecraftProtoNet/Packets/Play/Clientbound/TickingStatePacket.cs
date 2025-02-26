using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class TickingStatePacket : Packet
{
    public override int PacketId => 0x78;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public TickingState Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new TickingState
        {
            TickRate = buffer.ReadFloat(),
            IsFrozen = buffer.ReadBoolean()
        };
    }

    public class TickingState
    {
        public required float TickRate { get; set; }
        public required bool IsFrozen { get; set; }
    }
}
