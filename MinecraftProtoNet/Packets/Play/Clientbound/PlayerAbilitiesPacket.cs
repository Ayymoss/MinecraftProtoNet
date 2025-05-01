using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x39, ProtocolState.Play)]
public class PlayerAbilitiesPacket : IClientboundPacket
{
    public AbilityFlag Flag { get; set; }
    public float FlyingSpeed { get; set; }
    public float FieldOfViewerModifier { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Flag = (AbilityFlag)buffer.ReadUnsignedByte();
        FlyingSpeed = buffer.ReadFloat();
        FieldOfViewerModifier = buffer.ReadFloat();
    }

    public enum AbilityFlag : byte
    {
        None = 0x00,
        Invulnerable = 0x01,
        Flying = 0x02,
        AllowFlying = 0x04,
        CreativeMode = 0x08
    }
}
