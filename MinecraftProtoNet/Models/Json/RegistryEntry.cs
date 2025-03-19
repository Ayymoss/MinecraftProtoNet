using System.Text.Json.Serialization;

namespace MinecraftProtoNet.Models.Json;

public class RegistryEntry
{
    [JsonPropertyName("protocol_id")] public int ProtocolId { get; set; }
}
