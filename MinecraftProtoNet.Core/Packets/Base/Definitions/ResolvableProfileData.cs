using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Base.Definitions;

/// <summary>
/// A game profile as carried by the <c>resolvable_profile</c> data component / entity data serializer: either a
/// fully resolved profile, or a partial one the client is expected to resolve, plus an optional skin patch.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/item/component/ResolvableProfile.java:72-74
/// STREAM_CODEC = composite(either(GAME_PROFILE, Partial.STREAM_CODEC), unpack, PlayerSkin.Patch.STREAM_CODEC, skinPatch)
///
/// This is what backs player heads and — the reason it matters here — <c>minecraft:mannequin</c> NPCs, whose
/// identity and skin live entirely in this field.
/// </summary>
public sealed record ResolvableProfileData(
    string? Name,
    Guid? Uuid,
    IReadOnlyList<ProfileProperty> Properties,
    bool IsResolved,
    SkinPatch Skin)
{
    /// <summary>
    /// Reads the 26.2 layout.
    ///
    /// Getting this wrong is not a locally contained mistake: entity data is a flat stream of fields, so
    /// misreading this one desynchronises every field after it in the same packet.
    /// </summary>
    public static ResolvableProfileData Read(ref PacketBufferReader buffer)
    {
        // either(): true selects the LEFT codec (a complete GameProfile).
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java:481
        var isResolved = buffer.ReadBoolean();

        string? name;
        Guid? uuid;

        if (isResolved)
        {
            // GAME_PROFILE = composite(UUID, id, PLAYER_NAME, name, GAME_PROFILE_PROPERTIES, properties)
            // Reference: ByteBufCodecs.java:235
            uuid = buffer.ReadUuid();
            name = buffer.ReadString();
        }
        else
        {
            // Partial = composite(optional(PLAYER_NAME), name, optional(UUID), id, GAME_PROFILE_PROPERTIES, properties)
            // Reference: ResolvableProfile.java:93
            name = buffer.ReadBoolean() ? buffer.ReadString() : null;
            uuid = buffer.ReadBoolean() ? buffer.ReadUuid() : null;
        }

        // GAME_PROFILE_PROPERTIES: count, then name / value / nullable signature.
        // Reference: ByteBufCodecs.java:207-220
        var propertyCount = buffer.ReadVarInt();
        var properties = new List<ProfileProperty>(Math.Max(0, propertyCount));
        for (var i = 0; i < propertyCount; i++)
        {
            var propertyName = buffer.ReadString();
            var propertyValue = buffer.ReadString();
            var signature = buffer.ReadBoolean() ? buffer.ReadString() : null;
            properties.Add(new ProfileProperty(propertyName, propertyValue, signature));
        }

        return new ResolvableProfileData(name, uuid, properties, isResolved, SkinPatch.Read(ref buffer));
    }

    /// <summary>The base64 "textures" property, when the profile carries one.</summary>
    public string? TexturesValue =>
        Properties.FirstOrDefault(p => p.Name == "textures")?.Value;
}

public sealed record ProfileProperty(string Name, string Value, string? Signature);

/// <summary>
/// Per-profile skin overrides. Each texture is an identifier, and the model flag is true for the slim ("Alex")
/// arm model.
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/entity/player/PlayerSkin.java:32
/// </summary>
public sealed record SkinPatch(string? Body, string? Cape, string? Elytra, bool? SlimModel)
{
    public static SkinPatch Read(ref PacketBufferReader buffer)
    {
        var body = buffer.ReadBoolean() ? buffer.ReadString() : null;
        var cape = buffer.ReadBoolean() ? buffer.ReadString() : null;
        var elytra = buffer.ReadBoolean() ? buffer.ReadString() : null;
        // PlayerModelType.STREAM_CODEC maps BOOL -> SLIM/WIDE. Reference: PlayerModelType.java:18
        bool? slim = buffer.ReadBoolean() ? buffer.ReadBoolean() : null;
        return new SkinPatch(body, cape, elytra, slim);
    }
}
