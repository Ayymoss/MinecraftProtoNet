using System.Numerics;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class SetDefaultSpawnPositionPacket : Packet
{
    public override int PacketId => 0x5B;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public SetDefaultSpawnPosition Payload { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Payload = new SetDefaultSpawnPosition
        {
            Location = buffer.ReadPosition(),
            Angle = buffer.ReadFloat()
        };
    }

    public class SetDefaultSpawnPosition
    {
        public required Vector3 Location { get; set; }
        public required float Angle { get; set; }
    }
}
