using System.Numerics;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x5B, ProtocolState.Play)]
public class SetDefaultSpawnPositionPacket : IClientPacket
{
    public Vector3 Location { get; set; }
    public float Angle { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Location = buffer.ReadPosition();
        Angle = buffer.ReadFloat();
    }
}
