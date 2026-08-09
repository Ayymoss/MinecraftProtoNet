using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// The server is redirecting us to a different host — reconnect there rather than to the original address.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/common/ClientboundTransferPacket.java
///   record ClientboundTransferPacket(String host, int port) — readUtf() then readVarInt()
///
/// This was missing entirely, which is why the bot cannot hold a DIRECT connection to Hypixel: clicking a
/// hub in the Hub Selector redirects the client, we ignored the packet, and the server closed us ~7 seconds
/// later. Measured 2026-08-09: 14 consecutive launches, 0 trading cycles, every one dying at the hub switch.
/// Running through SniffCraft hid it because the proxy normalises the address to a single endpoint.
///
/// Packet id derived from the registration order in GameProtocols.java (ids are assigned by position):
/// CLIENTBOUND_TRANSFER sits at index 129 of the clientbound Play table.
/// </summary>
[Packet(0x81, ProtocolState.Play)]
public class TransferPacket : IClientboundPacket
{
    /// <summary>Host to reconnect to.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Port to reconnect to.</summary>
    public int Port { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Host = buffer.ReadString();
        Port = buffer.ReadVarInt();
    }
}
