using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

[Packet(0x00, ProtocolState.Configuration)]
public class ClientInformationPacket : IServerboundPacket
{
    public string Locale { get; set; } = "en_US";
    public byte ViewDistance { get; set; } = 10;
    public int ChatMode { get; set; } = 0; // Enabled
    public bool ChatColors { get; set; } = true;
    public byte DisplayedSkinParts { get; set; } = 0x7F; // All
    public int MainHand { get; set; } = 1; // Right
    public bool EnableTextFiltering { get; set; } = false;
    public bool AllowServerListings { get; set; } = true;
    public int ParticleStatus { get; set; } = 0; // All

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Locale);
        buffer.WriteUnsignedByte(ViewDistance);
        buffer.WriteVarInt(ChatMode);
        buffer.WriteBoolean(ChatColors);
        buffer.WriteUnsignedByte(DisplayedSkinParts);
        buffer.WriteVarInt(MainHand);
        buffer.WriteBoolean(EnableTextFiltering);
        buffer.WriteBoolean(AllowServerListings);
        // Added in newer versions (1.20.5+ / 775)
        buffer.WriteVarInt(ParticleStatus); 
    }
}
