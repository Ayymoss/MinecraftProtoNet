using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x6B, ProtocolState.Play, true)]
public class SetTimePacket : IClientPacket
{
    public long WorldAge { get; set; }
    public long TimeOfDay { get; set; }
    public bool TimeOfDayIncreasing { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        WorldAge = buffer.ReadSignedLong();
        TimeOfDay = buffer.ReadSignedLong();
        TimeOfDayIncreasing = buffer.ReadBoolean();
    }
}
