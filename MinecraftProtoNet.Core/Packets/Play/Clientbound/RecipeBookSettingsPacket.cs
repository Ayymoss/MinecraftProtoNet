using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Contains recipe book settings for all recipe book types.
/// Each type has two boolean settings: open and filtering.
/// </summary>
[Packet(0x4B, ProtocolState.Play)]
public class RecipeBookSettingsPacket : IClientboundPacket
{
    // Crafting settings
    public bool CraftingOpen { get; set; }
    public bool CraftingFiltering { get; set; }

    // Furnace settings
    public bool FurnaceOpen { get; set; }
    public bool FurnaceFiltering { get; set; }

    // Blast Furnace settings
    public bool BlastFurnaceOpen { get; set; }
    public bool BlastFurnaceFiltering { get; set; }

    // Smoker settings
    public bool SmokerOpen { get; set; }
    public bool SmokerFiltering { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        CraftingOpen = buffer.ReadBoolean();
        CraftingFiltering = buffer.ReadBoolean();
        FurnaceOpen = buffer.ReadBoolean();
        FurnaceFiltering = buffer.ReadBoolean();
        BlastFurnaceOpen = buffer.ReadBoolean();
        BlastFurnaceFiltering = buffer.ReadBoolean();
        SmokerOpen = buffer.ReadBoolean();
        SmokerFiltering = buffer.ReadBoolean();
    }
}
