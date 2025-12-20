using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to open an inventory screen (chest, furnace, crafting table, etc.).
/// </summary>
[Packet(0x3A, ProtocolState.Play)]
public class OpenScreenPacket : IClientboundPacket
{
    /// <summary>
    /// A unique container ID for this screen instance.
    /// </summary>
    public int ContainerId { get; set; }
    
    /// <summary>
    /// The menu type from the registry (determines the UI layout).
    /// </summary>
    public int MenuType { get; set; }
    
    /// <summary>
    /// The title displayed at the top of the screen.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ContainerId = buffer.ReadVarInt();
        MenuType = buffer.ReadVarInt();
        Title = buffer.ReadChatComponent();
    }
}
