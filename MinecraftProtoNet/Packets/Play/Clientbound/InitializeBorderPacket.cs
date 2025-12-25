using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x2A, ProtocolState.Play)]
public class InitializeBorderPacket : IClientboundPacket
{
    public double X { get; set; }
    public double Z { get; set; }
    public double OldDiameter { get; set; }
    public double NewDiameter { get; set; }
    public long Speed { get; set; }
    public int PortalTeleportBoundary { get; set; }
    public int WarningBlocks { get; set; }
    public int WarningTime { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        X = buffer.ReadDouble();
        Z = buffer.ReadDouble();
        OldDiameter = buffer.ReadDouble();
        NewDiameter = buffer.ReadDouble();
        Speed = buffer.ReadVarLong();
        PortalTeleportBoundary = buffer.ReadVarInt();
        WarningBlocks = buffer.ReadVarInt();
        WarningTime = buffer.ReadVarInt();
    }
}
