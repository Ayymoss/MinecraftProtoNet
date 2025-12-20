using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to signal that the client should enter the configuration phase.
/// This has no payload - it's just a signal.
/// </summary>
[Packet(0x75, ProtocolState.Play)]
public class StartConfigurationPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // No payload - this is a unit/singleton packet
    }
}
