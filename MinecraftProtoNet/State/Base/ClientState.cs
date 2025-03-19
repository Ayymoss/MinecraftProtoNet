using System.Collections.Concurrent;
using System.Collections.Frozen;
using MinecraftProtoNet.Models.Json;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.NBT.Tags;
using BlockState = MinecraftProtoNet.Models.World.Chunk.BlockState;

namespace MinecraftProtoNet.State.Base;

public class ClientState
{
    public Level Level { get; set; } = new();
    public Player LocalPlayer { get; set; } = new() { Entity = new Entity() };
    public ConcurrentDictionary<string, Dictionary<string, NbtTag?>> Registry { get; set; } = [];

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
