using System.Text.Json;

namespace MinecraftProtoNet.Core.Packets.Base.Definitions;

/// <summary>
/// A single signed property on a Mojang GameProfile (most commonly "textures" for skins).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java GAME_PROFILE_PROPERTIES
/// </summary>
public record PlayerProfileProperty(string Name, string Value, string? Signature);

/// <summary>
/// Deserialized ResolvableProfile data captured from a Profile (0x46) slot component.
/// Used by the web UI to render player-head icons for skull items (including Hypixel's
/// custom-skin items in Bazaar menus).
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ResolvableProfile.java
/// </summary>
public record PlayerProfile(Guid? Uuid, string? Name, List<PlayerProfileProperty> Properties)
{
    /// <summary>
    /// Decodes the "textures" property's base64-JSON value and returns the SKIN texture URL if present.
    /// Returns null when the property is absent, the base64 is malformed, or the JSON shape is unexpected.
    /// </summary>
    public string? GetSkinTextureUrl()
    {
        var texProp = Properties.FirstOrDefault(p => p.Name == "textures");
        if (texProp is null) return null;

        try
        {
            var bytes = Convert.FromBase64String(texProp.Value);
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("textures", out var textures) &&
                textures.TryGetProperty("SKIN", out var skin) &&
                skin.TryGetProperty("url", out var url) &&
                url.ValueKind == JsonValueKind.String)
            {
                return url.GetString();
            }
        }
        catch
        {
            // Malformed property — skip silently, UI will fall back to placeholder.
        }
        return null;
    }
}
