using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x1F, ProtocolState.Play)]
public class MovePlayerRotationPacket : IServerboundPacket
{
    public required float Yaw { get; set; }
    public required float Pitch { get; set; }
    public required MovementFlags Flags { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteFloat(Yaw);
        buffer.WriteFloat(Pitch);
        buffer.WriteUnsignedByte((byte)Flags);
    }
}
