using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x64, ProtocolState.Play, true)]
public class SetEntityMotionPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Vector3<short> Velocity { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        Velocity = new Vector3<short>
        {
            X = buffer.ReadSignedShort(),
            Y = buffer.ReadSignedShort(),
            Z = buffer.ReadSignedShort()
        };
    }
}
