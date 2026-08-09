using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

[Packet(0x00, ProtocolState.Configuration)]
public class ClientInformationPacket : IServerboundPacket
{
    // Defaults below are what a real 26.2 client sent to Hypixel, taken byte-for-byte from a capture:
    //   vanilla  0x00 0x05 "en_us" 0x20 0x00 0x01 0x7F 0x01 0x01 0x01 0x00
    //   ours     0x00 0x05 "en_US" 0x0A 0x00 0x01 0x7F 0x01 0x00 0x01 0x00
    // We diverged in three fields: the locale was upper-cased ("en_us" is what the client actually
    // sends), the view distance was 10 against vanilla's 32, and text filtering was false not true.
    // Set MCPROTO_LEGACY_CLIENTINFO=1 to restore the old values for an A/B against these.
    private static readonly bool Legacy =
        Environment.GetEnvironmentVariable("MCPROTO_LEGACY_CLIENTINFO") == "1";

    public string Locale { get; set; } = Legacy ? "en_US" : "en_us";
    public byte ViewDistance { get; set; } = Legacy ? (byte)10 : (byte)32;
    public int ChatMode { get; set; } = 0; // Enabled
    public bool ChatColors { get; set; } = true;
    public byte DisplayedSkinParts { get; set; } = 0x7F; // All
    public int MainHand { get; set; } = 1; // Right
    public bool EnableTextFiltering { get; set; } = !Legacy;
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
