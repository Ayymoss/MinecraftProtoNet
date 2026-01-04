using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x70, ProtocolState.Play, true)]
public class SetTimePacket : IClientboundPacket
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
