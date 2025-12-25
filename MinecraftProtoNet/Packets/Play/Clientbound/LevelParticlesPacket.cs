using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x2E, ProtocolState.Play)]
public class LevelParticlesPacket : IClientboundPacket
{
    public bool LongDistance { get; set; }
    public bool AlwaysVisible { get; set; }
    public Vector3<double> Position { get; set; }
    public Vector3<float> Offset { get; set; }
    public float MaxSpeed { get; set; }
    public int ParticleCount { get; set; }
    // ParticleId and Data are read from particle codec - skipped for now

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
        ParticleCount = buffer.ReadSignedInt(); // Fixed: was VarInt, should be Int

        // Particle data comes last (complex particle codec)
        // Skip remaining bytes for particle data
        _ = buffer.ReadRestBuffer();
    }
}

