using System.Collections.Concurrent;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.NBT.Tags;

namespace MinecraftProtoNet.State.Base;

public class ClientState
{
    public Level Level { get; set; } = new();
    public Player Player { get; set; } = new();
    public ConcurrentDictionary<string, Dictionary<string, NbtTag?>> Registry { get; set; } = [];

    public static Dictionary<int, BlockState> BlockStateRegistry { get; private set; } = new();
    public static Dictionary<int, Biome> BiomeRegistry { get; private set; } = new();

    public static void InitializeBlockStateRegistry(Dictionary<int, BlockState> blockStates)
    {
        BlockStateRegistry = new Dictionary<int, BlockState>(blockStates);
    }

    public static void InitializeBiomeRegistry(Dictionary<int, Biome> biomes)
    {
        BiomeRegistry = new Dictionary<int, Biome>(biomes);
    }
}
