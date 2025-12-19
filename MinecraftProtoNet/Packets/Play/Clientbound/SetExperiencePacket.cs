using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x66, ProtocolState.Play)]
public class SetExperiencePacket : IClientboundPacket
{
    public float ExperienceBar { get; set; }
    public int Level { get; set; }
    public int TotalExperience { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ExperienceBar = buffer.ReadFloat();
        Level = buffer.ReadVarInt();
        TotalExperience = buffer.ReadVarInt();
    }
}
