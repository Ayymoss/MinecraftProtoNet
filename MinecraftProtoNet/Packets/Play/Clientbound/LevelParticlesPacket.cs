using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x29, ProtocolState.Play)]
public class LevelParticlesPacket : IClientboundPacket
{
    public bool LongDistance { get; set; }
    public bool AlwaysVisible { get; set; }
    public Vector3<double> Position { get; set; }
    public Vector3<float> Offset { get; set; }
    public float MaxSpeed { get; set; }
    public int ParticleCount { get; set; }
    public int ParticleId { get; set; }
    public byte[] Data { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        LongDistance = buffer.ReadBoolean();
        AlwaysVisible = buffer.ReadBoolean();

        var positionX = buffer.ReadDouble();
        var positionY = buffer.ReadDouble();
        var positionZ = buffer.ReadDouble();
        Position = new Vector3<double>(positionX, positionY, positionZ);

        var offsetX = buffer.ReadFloat();
        var offsetY = buffer.ReadFloat();
        var offsetZ = buffer.ReadFloat();
        Offset = new Vector3<float>(offsetX, offsetY, offsetZ);

        MaxSpeed = buffer.ReadFloat();
        ParticleCount = buffer.ReadVarInt();
        ParticleId = buffer.ReadVarInt();
        // TODO: Data ?? - Need to implement. https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/Particles
    }
}
