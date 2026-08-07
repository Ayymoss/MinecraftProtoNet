using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// The 26.x server-driven dialog: a screen the server defines and pushes at the client, with its own buttons
/// and inputs.
///
/// The payload is a registry-or-inline dialog definition that we deliberately do NOT model. The bot has no
/// business answering a dialog it did not ask for, so the useful behaviour is to record that one arrived and
/// keep the bytes for a human to decode later.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/common/ClientboundShowDialogPacket.java
/// </summary>
[Packet(0x8C, ProtocolState.Play)]
public class ShowDialogPacket : IClientboundPacket
{
    /// <summary>Raw payload, kept verbatim so an unrecognised dialog can still be analysed after the fact.</summary>
    public byte[] RawPayload { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        RawPayload = buffer.ReadRestBuffer().ToArray();
    }

    public string PayloadHex => Convert.ToHexString(RawPayload.AsSpan(0, Math.Min(RawPayload.Length, 256)));
}
