using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Core.Models.Json;

public class BlockRoot
{
    /// <summary>
    /// The block-level "definition" from the vanilla data report. Holds the block-class identity
    /// (e.g. minecraft:slab, minecraft:ladder, minecraft:liquid) shared by all states of the block,
    /// equivalent to the Java Block subclass. Used for non-heuristic block classification.
    /// </summary>
    [JsonPropertyName("definition")] public BlockDefinitionJson? Definition { get; init; }

    [JsonPropertyName("states")] public List<BlockState> States { get; init; } = [];
}

public class BlockDefinitionJson
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("fluid")] public string? Fluid { get; init; }
}
