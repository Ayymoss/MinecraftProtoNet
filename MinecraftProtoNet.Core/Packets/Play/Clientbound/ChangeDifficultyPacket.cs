using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x0A, ProtocolState.Play)]
public class ChangeDifficultyPacket : IClientboundPacket
{
    public DifficultyFlag Difficulty { get; set; }
    public bool DifficultyLocked { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
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
