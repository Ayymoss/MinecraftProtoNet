namespace MinecraftProtoNet.Core.Core;

/// <summary>
/// Entity type IDs for Minecraft 1.21.x / Protocol 775.
/// These IDs may change between protocol versions.
/// </summary>
public static class EntityTypes
{
    /// <summary>
    /// Player entity type ID for Minecraft 26.1.
    /// Verify with entity registry if protocol version changes.
    /// </summary>
    public const int Player = 155;

    /// <summary>
    /// Villager entity type ID. From StaticFiles/reports/registries.json -> minecraft:entity_type ->
    /// minecraft:villager -> protocol_id. Verify with the entity registry if the protocol version changes.
    /// </summary>
    public const int Villager = 140;
}
