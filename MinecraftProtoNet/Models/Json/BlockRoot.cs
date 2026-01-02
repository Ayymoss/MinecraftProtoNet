using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class BlockRoot
{
    [JsonPropertyName("states")] public List<BlockState> States { get; init; } = [];
}
