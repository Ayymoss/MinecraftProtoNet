using System.Collections.Concurrent;
using System.Collections.Frozen;
using MinecraftProtoNet.Models.Json;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.NBT.Tags;
using BlockState = MinecraftProtoNet.Models.World.Chunk.BlockState;

namespace MinecraftProtoNet.State.Base;

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
}

public class ClientState
{
    public Level Level { get; set; } = new();
    public Player LocalPlayer { get; set; } = new() { Entity = new Entity() };
    public ConcurrentDictionary<string, Dictionary<string, NbtTag?>> Registry { get; set; } = [];
    
    /// <summary>
    /// Registry for non-player entities (mobs, villagers, NPCs, etc.).
    /// </summary>
    public WorldEntityRegistry WorldEntities { get; } = new();

    
    /// <summary>
    /// Server-provided settings from the Login packet.
    /// </summary>
    public ServerSettings ServerSettings { get; } = new();

    public static FrozenDictionary<int, BlockState> BlockStateRegistry { get; private set; } = null!;
    public static FrozenDictionary<int, Biome> BiomeRegistry { get; private set; } = null!;
    public static FrozenDictionary<int, string> ItemRegistry { get; private set; } = null!;

    public static void InitializeBlockStateRegistry(Dictionary<int, BlockState> blockStates)
    {
        BlockStateRegistry = blockStates.ToFrozenDictionary();
    }

    public static void InitializeBiomeRegistry(Dictionary<int, Biome> biomes)
    {
        BiomeRegistry = biomes.ToFrozenDictionary();
    }

    public static void InitialiseItemRegistry(Dictionary<int, string> registry)
    {
        ItemRegistry = registry.ToFrozenDictionary();
    }
}
