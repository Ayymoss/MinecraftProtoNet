using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.World;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x71, ProtocolState.Play, true)]
public sealed class SetTimePacket : IClientboundPacket
{
    public long GameTime { get; set; }
    public Dictionary<int, ClockState> ClockUpdates { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        GameTime = buffer.ReadSignedLong();
        var count = buffer.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            var clockId = buffer.ReadVarInt();
            var totalTicks = buffer.ReadVarLong();
            var paused = buffer.ReadBoolean();
            ClockUpdates[clockId] = new ClockState(totalTicks, paused);
        }
    }
}
