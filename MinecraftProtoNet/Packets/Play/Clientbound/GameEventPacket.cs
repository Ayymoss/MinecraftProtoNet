using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x26, ProtocolState.Play)]
public class GameEventPacket : IClientboundPacket
{
    public EventFlag Event { get; set; }
    public float Value { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Event = (EventFlag)buffer.ReadUnsignedByte();
        Value = buffer.ReadFloat();
    }

    public enum EventFlag
    {
        NoRespawnBlockAvailable = 0,
        BeginRain = 1,
        EndRain = 2,
        ChangeGameMode = 3,
        WinGame = 4,
        DemoEvent = 5,
        ArrowHitPlayer = 6,
        RainLevelChange = 7,
        ThunderLevelChange = 8,
        PlayPufferfishStingSound = 9,
        PlayElderGuardianCurseEffects = 10,
        EnableRespawnScreen = 11,
        LimitedCrafting = 12,
        StartWaitingForLevelChunks = 13,
    }
}
