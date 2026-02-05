using System.Text.Json;
using MinecraftProtoNet.Core.Models.Json;
using BlockState = MinecraftProtoNet.Core.Models.World.Chunk.BlockState;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Loads static game data from JSON files in the StaticFiles directory.
/// </summary>
public class RegistryDataLoader : IRegistryDataLoader
{
    private const string BlocksFileName = "blocks.json";
    private const string RegistriesFileName = "registries.json";

    private readonly string _staticFilesPath = Path.Combine(AppContext.BaseDirectory, "StaticFiles");

    /// <inheritdoc />
    public async Task<Dictionary<int, BlockState>> LoadBlockStatesAsync()
    {
        var filePath = Path.Combine(_staticFilesPath, BlocksFileName);
        var json = await File.ReadAllTextAsync(filePath);
        var blockData = JsonSerializer.Deserialize<Dictionary<string, BlockRoot>>(json) ?? [];

        return blockData
            .SelectMany(kvp => kvp.Value.States.Select(state => new { BlockName = kvp.Key, StateId = state.Id, Properties = state.Properties }))
            .ToDictionary(x => x.StateId, x => new BlockState(x.StateId, x.BlockName, x.Properties));
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, string>> LoadItemsAsync()
    {
        var filePath = Path.Combine(_staticFilesPath, RegistriesFileName);
        var json = await File.ReadAllTextAsync(filePath);
        var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryRoot>>(json) ?? [];

        return registry["minecraft:item"].Entries
            .ToDictionary(x => x.Value.ProtocolId, x => x.Key);
    }
}
