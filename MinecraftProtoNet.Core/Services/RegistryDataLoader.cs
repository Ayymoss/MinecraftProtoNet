using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Core.Models.Json;
using BlockState = MinecraftProtoNet.Core.Models.World.Chunk.BlockState;
using BlockDefinition = MinecraftProtoNet.Core.Models.World.Chunk.BlockDefinition;
using BlockHardness = MinecraftProtoNet.Core.Models.World.Chunk.BlockHardness;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Loads static game data from JSON files in the StaticFiles directory.
/// </summary>
public class RegistryDataLoader : IRegistryDataLoader
{
    private const string BlocksFileName = "blocks.json";
    private const string RegistriesFileName = "registries.json";
    private const string BlockHardnessFileName = "block_hardness.json";

    private readonly string _staticFilesPath = Path.Combine(AppContext.BaseDirectory, @"StaticFiles\reports");

    // Parsed once and reused so LoadBlockStatesAsync and LoadBlockDefinitionsAsync don't re-read the 6 MB report.
    private Dictionary<string, BlockRoot>? _blockData;

    private async Task<Dictionary<string, BlockRoot>> GetBlockDataAsync()
    {
        if (_blockData != null) return _blockData;
        var filePath = Path.Combine(_staticFilesPath, BlocksFileName);
        var json = await File.ReadAllTextAsync(filePath);
        _blockData = JsonSerializer.Deserialize<Dictionary<string, BlockRoot>>(json) ?? [];
        return _blockData;
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, BlockState>> LoadBlockStatesAsync()
    {
        var blockData = await GetBlockDataAsync();

        return blockData
            .SelectMany(kvp => kvp.Value.States.Select(state => new { BlockName = kvp.Key, StateId = state.Id, Properties = state.Properties }))
            .ToDictionary(x => x.StateId, x => new BlockState(x.StateId, x.BlockName, x.Properties));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, BlockDefinition>> LoadBlockDefinitionsAsync()
    {
        var blockData = await GetBlockDataAsync();

        return blockData
            .Where(kvp => kvp.Value.Definition?.Type != null)
            .ToDictionary(kvp => kvp.Key, kvp => new BlockDefinition(kvp.Value.Definition!.Type!, kvp.Value.Definition.Fluid));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, BlockHardness>> LoadBlockHardnessAsync()
    {
        var filePath = Path.Combine(_staticFilesPath, BlockHardnessFileName);
        var json = await File.ReadAllTextAsync(filePath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, BlockHardnessJson>>(json) ?? [];
        return raw.ToDictionary(kvp => kvp.Key, kvp => new BlockHardness(kvp.Value.Hardness, kvp.Value.RequiresCorrectTool));
    }

    private sealed class BlockHardnessJson
    {
        [JsonPropertyName("hardness")] public float Hardness { get; init; }
        [JsonPropertyName("requiresCorrectTool")] public bool RequiresCorrectTool { get; init; }
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

    /// <inheritdoc />
    public async Task<Dictionary<int, string>> LoadEntityTypesAsync()
    {
        var filePath = Path.Combine(_staticFilesPath, RegistriesFileName);
        var json = await File.ReadAllTextAsync(filePath);
        var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryRoot>>(json) ?? [];

        return registry["minecraft:entity_type"].Entries
            .ToDictionary(x => x.Value.ProtocolId, x => x.Key);
    }

    /// <summary>
    /// Loads the attribute registry (protocol id -> name) from the generated report.
    ///
    /// minecraft:attribute is a BUILT-IN registry, so unlike dimensions or biomes the server never sends it —
    /// the ids in UpdateAttributes can only be resolved from the report for the matching protocol version.
    /// </summary>
    public async Task<Dictionary<int, string>> LoadAttributesAsync()
    {
        var filePath = Path.Combine(_staticFilesPath, RegistriesFileName);
        var json = await File.ReadAllTextAsync(filePath);
        var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryRoot>>(json) ?? [];

        return registry["minecraft:attribute"].Entries
            .ToDictionary(x => x.Value.ProtocolId, x => x.Key);
    }
}
