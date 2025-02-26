using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class ChangeDifficultyPacket : Packet
{
    public override int PacketId => 0x0B;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public DifficultyFlag Difficulty { get; set; }
    public bool DifficultyLocked { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Difficulty = (DifficultyFlag)buffer.ReadUnsignedByte();
        DifficultyLocked = buffer.ReadBoolean();
    }

    public enum DifficultyFlag : byte
    {
        Peaceful = 0x00,
        Easy = 0x01,
        Normal = 0x02,
        Hard = 0x03
    }
}
