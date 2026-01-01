using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

/// <summary>
/// Sent by the client to close an open container.
/// </summary>
[Packet(0x12, ProtocolState.Play)]
public class CloseContainerPacket : IServerboundPacket
{
    /// <summary>
    /// The container ID to close. 0 closes the player inventory.
    /// </summary>
    public int ContainerId { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(ContainerId);
    }
}
