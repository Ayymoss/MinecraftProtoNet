using System.Collections.Concurrent;
using System.Collections.Frozen;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Services;
using BlockState = MinecraftProtoNet.Core.Models.World.Chunk.BlockState;

namespace MinecraftProtoNet.Core.State.Base;

/// <summary>
/// Stores server-provided configuration settings.
/// </summary>
public class ServerSettings
{
    /// <summary>
    /// Whether the server requires signed chat messages.
    /// </summary>
    public bool EnforcesSecureChat { get; set; }
    
    /// <summary>
    /// Whether the server is marked as hardcore mode.
    /// </summary>
    public bool IsHardcore { get; set; }
    
    /// <summary>
    /// The view distance configured by the server.
    /// </summary>
    public int ViewDistance { get; set; }
    
    /// <summary>
    /// The simulation distance configured by the server.
    /// </summary>
    public int SimulationDistance { get; set; }

    /// <summary>
    /// Whether the server is behind ViaVersion (detected via vv:server_details plugin channel).
    /// When true, ChatSessionUpdate should NOT be sent even if EnforcesSecureChat is true,
    /// because ViaVersion backends may not support it.
    /// </summary>
    public bool HasViaVersion { get; set; }
}

public class ClientState
{
    public Level Level { get; set; } = new();
    public Player LocalPlayer { get; set; } = new() { Entity = new Entity() };
    public ConcurrentDictionary<string, Dictionary<string, NbtTag?>> Registry { get; set; } = [];

    /// <summary>
    /// Ordered list of entry keys per registry, preserving protocol-order indices.
    /// Used to resolve registry IDs (e.g., DamageEventPacket.SourceTypeId) to entry names.
    /// </summary>
    public ConcurrentDictionary<string, List<string>> RegistryKeyOrder { get; set; } = [];
    
    /// <summary>
    /// Registry for non-player entities (mobs, villagers, NPCs, etc.).
    /// </summary>
    public WorldEntityRegistry WorldEntities { get; } = new();

    
    /// <summary>
    /// Server-provided settings from the Login packet.
    /// </summary>
    public ServerSettings ServerSettings { get; } = new();

    /// <summary>
    /// Bot-specific configuration settings.
    /// </summary>
    public BotSettings BotSettings { get; } = new();

    /// <summary>
    /// The hostname/IP of the currently connected server. Null when disconnected.
    /// Used by the humanizer to determine local vs remote server.
    /// </summary>
    public string? ConnectedServerHost { get; set; }

    /// <summary>
    /// The last disconnect message received from the server (translated visible text).
    /// Set by DisconnectPacket / LoginDisconnectPacket handlers so the UI can display why
    /// the server closed the connection (e.g. "you are banned", rate limit, kick reason).
    /// </summary>
    public string? LastDisconnectReason { get; set; }

    /// <summary>
    /// The translate key (e.g. "multiplayer.disconnect.banned") of the last disconnect, if one
    /// was provided. Lets callers key off known keys instead of parsing the visible text.
    /// </summary>
    public string? LastDisconnectTranslateKey { get; set; }

    /// <summary>
    /// UTC timestamp of the last disconnect. Pairs with <see cref="LastDisconnectReason"/>.
    /// </summary>
    public DateTimeOffset? LastDisconnectAt { get; set; }

    /// <summary>
    /// Gets the camera entity (the entity used for rendering/view calculations).
    /// Typically the same as LocalPlayer.Entity, but can differ (e.g., spectator mode).
    /// Equivalent to Java's Minecraft.getCameraEntity().
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java:74
    /// </summary>
    public Entity? GetCameraEntity()
    {
        // For now, camera entity is always the local player's entity.
        // In spectator mode or other cases, this could be different.
        return LocalPlayer.HasEntity ? LocalPlayer.Entity : null;
    }

    public static FrozenDictionary<int, BlockState> BlockStateRegistry { get; private set; } = null!;
    public static FrozenDictionary<int, Biome> BiomeRegistry { get; private set; } = null!;
    public static FrozenDictionary<int, string> ItemRegistry { get; private set; } = null!;
    public static FrozenDictionary<int, string> EntityTypeRegistry { get; private set; } = null!;
    public static BlockTagRegistry BlockTags { get; private set; } = new();

    /// <summary>
    /// Block-class identity by block name (e.g. minecraft:slab, minecraft:liquid), sourced from the
    /// vanilla data report. Backs non-heuristic block classification on <see cref="BlockState"/>.
    /// </summary>
    public static FrozenDictionary<string, BlockDefinition> BlockDefinitions { get; private set; } =
        FrozenDictionary<string, BlockDefinition>.Empty;

    /// <summary>
    /// Per-block destroy speed (hardness) + requiresCorrectToolForDrops, generated from the MC 26.2
    /// source. Backs <see cref="BlockState.DestroySpeed"/> / <see cref="BlockState.RequiresCorrectToolForDrops"/>.
    /// </summary>
    public static FrozenDictionary<string, BlockHardness> BlockHardness { get; private set; } =
        FrozenDictionary<string, BlockHardness>.Empty;

    public static void InitializeBlockTags()
    {
        BlockTags.Initialize();
    }

    public static void InitializeBlockStateRegistry(Dictionary<int, BlockState> blockStates)
    {
        BlockStateRegistry = blockStates.ToFrozenDictionary();
    }

    public static void InitializeBlockDefinitions(Dictionary<string, BlockDefinition> definitions)
    {
        BlockDefinitions = definitions.ToFrozenDictionary();
    }

    public static void InitializeBlockHardness(Dictionary<string, BlockHardness> hardness)
    {
        BlockHardness = hardness.ToFrozenDictionary();
    }

    /// <summary>
    /// Maps enchantment id (e.g. minecraft:efficiency) to its holder index in the server-sent
    /// enchantment registry. The index is the key used in an item's Enchantments component map.
    /// </summary>
    public static FrozenDictionary<string, int> EnchantmentRegistry { get; private set; } =
        FrozenDictionary<string, int>.Empty;

    public static void InitializeEnchantmentRegistry(Dictionary<string, int> registry)
    {
        EnchantmentRegistry = registry.ToFrozenDictionary();
    }

    /// <summary>
    /// Maps mob-effect id (e.g. minecraft:haste) to its holder index in the server-sent mob_effect
    /// registry — the id used in UpdateMobEffect packets and <see cref="Entity"/> active effects.
    /// </summary>
    public static FrozenDictionary<string, int> MobEffectRegistry { get; private set; } =
        FrozenDictionary<string, int>.Empty;

    public static void InitializeMobEffectRegistry(Dictionary<string, int> registry)
    {
        MobEffectRegistry = registry.ToFrozenDictionary();
    }

    public static void InitializeBiomeRegistry(Dictionary<int, Biome> biomes)
    {
        BiomeRegistry = biomes.ToFrozenDictionary();
    }

    public static void InitialiseItemRegistry(Dictionary<int, string> registry)
    {
        ItemRegistry = registry.ToFrozenDictionary();
    }

    public static void InitializeEntityTypeRegistry(Dictionary<int, string> registry)
    {
        EntityTypeRegistry = registry.ToFrozenDictionary();
    }
}
