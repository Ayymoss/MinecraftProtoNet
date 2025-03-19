using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class RegistryRoot
{
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("protocol_id")] public int ProtocolId { get; set; }
    [JsonPropertyName("entries")] public Dictionary<string, RegistryEntry> Entries { get; set; }
}
