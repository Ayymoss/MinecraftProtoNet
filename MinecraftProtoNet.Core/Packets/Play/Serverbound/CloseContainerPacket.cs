using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

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
