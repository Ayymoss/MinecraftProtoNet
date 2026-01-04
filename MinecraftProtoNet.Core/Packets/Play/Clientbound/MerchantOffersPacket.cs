using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

/// <summary>
/// Sent by the server to populate merchant/villager trade offers.
/// </summary>
[Packet(0x33, ProtocolState.Play)]
public class MerchantOffersPacket : IClientboundPacket
{
    /// <summary>
    /// The container ID for the merchant window.
    /// </summary>
    public int ContainerId { get; set; }
    
    /// <summary>
    /// List of available trade offers.
    /// </summary>
    public List<MerchantOffer> Offers { get; set; } = new();
    
    /// <summary>
    /// Villager profession level (1-5).
    /// </summary>
    public int VillagerLevel { get; set; }
    
    /// <summary>
    /// Villager XP towards next level.
    /// </summary>
    public int VillagerXp { get; set; }
    
    /// <summary>
    /// Whether to show the level progress bar.
    /// </summary>
    public bool ShowProgress { get; set; }
    
    /// <summary>
    /// Whether the villager can restock trades.
    /// </summary>
    public bool CanRestock { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ContainerId = buffer.ReadVarInt();
        Offers = MerchantOffer.ReadList(ref buffer);
        VillagerLevel = buffer.ReadVarInt();
        VillagerXp = buffer.ReadVarInt();
        ShowProgress = buffer.ReadBoolean();
        CanRestock = buffer.ReadBoolean();
    }
}
