using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class BlockState
{
    [JsonPropertyName("id")] public int Id { get; init; }
}
