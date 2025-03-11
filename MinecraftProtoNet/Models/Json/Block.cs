using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class Block
{
    [JsonPropertyName("states")] public List<BlockState> States { get; init; } = [];
}
