using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Acknowledges a server-initiated return to the Configuration phase (reconfiguration), e.g. a Velocity/Bungee
/// backend switch. Sent in response to the clientbound StartConfiguration packet; the client then switches its
/// protocol state back to Configuration. No payload.
/// Reference: minecraft-26.2-REFERENCE-ONLY ClientPacketListener.handleConfigurationStart →
/// send(ServerboundConfigurationAcknowledgedPacket.INSTANCE). protocol_id 16 (packets.json).
/// </summary>
[Packet(0x10, ProtocolState.Play)]
public class ConfigurationAcknowledgedPacket : IServerboundPacket
{
    public void Serialize(ref PacketBufferWriter buffer)
    {
    }
}
