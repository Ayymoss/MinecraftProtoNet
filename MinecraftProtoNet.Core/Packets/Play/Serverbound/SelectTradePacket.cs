using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

/// <summary>
/// Sent by the client to select a trade in the merchant UI.
/// </summary>
[Packet(0x32, ProtocolState.Play)]
public class SelectTradePacket : IServerboundPacket
{
    /// <summary>
    /// The index of the selected trade offer.
    /// </summary>
    public int SelectedSlot { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(SelectedSlot);
    }
}
