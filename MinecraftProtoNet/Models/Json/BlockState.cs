using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class BlockState
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("properties")] public Dictionary<string, string> Properties { get; init; } = [];
}
