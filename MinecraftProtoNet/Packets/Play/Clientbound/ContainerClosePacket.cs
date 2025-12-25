using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to close a container/inventory window.
/// </summary>
[Packet(0x11, ProtocolState.Play)]
public class ContainerClosePacket : IClientboundPacket
{
    /// <summary>
    /// The container ID that was closed. 0 for player inventory.
    /// </summary>
    public int ContainerId { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ContainerId = buffer.ReadVarInt();
    }
}
