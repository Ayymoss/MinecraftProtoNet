using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class SetTimePacket : Packet
{
    public override int PacketId => 0x6B;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public SetTime Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new SetTime
        {
            WorldAge = buffer.ReadSignedLong(),
            TimeOfDay = buffer.ReadSignedLong(),
            TimeOfDayIncreasing = buffer.ReadBoolean()
        };
    }

    public class SetTime
    {
        public required long WorldAge { get; set; }
        public required long TimeOfDay { get; set; }
        public required bool TimeOfDayIncreasing { get; set; }
    }
}
