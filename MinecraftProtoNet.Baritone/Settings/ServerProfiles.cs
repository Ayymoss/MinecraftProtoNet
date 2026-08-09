using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Settings;

/// <summary>
/// What Baritone is permitted to do on a given server.
///
/// Baritone's defaults assume a vanilla world where the bot may break and place blocks to make a route work.
/// On a public minigame server it may not, and the failure is not a clean one: the pathfinder happily costs a
/// route through a pane of glass, walks to it, swings at it for the full movement timeout, gets nowhere, and
/// re-plans — the bot ends up drifting AWAY from its goal while visibly mining scenery in front of everyone.
/// Encoding the permissions per server turns that into a route the pathfinder never considers.
/// </summary>
/// <param name="Name">Shown in logs so it is obvious which profile a run picked up.</param>
/// <param name="AllowBreak">Whether Baritone may plan routes that require breaking a block.</param>
/// <param name="AllowPlace">Whether Baritone may plan routes that require placing a block.</param>
/// <param name="HostSuffixes">Host suffixes this profile claims; empty means it is the fallback.</param>
public sealed record ServerProfile(
    string Name,
    bool AllowBreak,
    bool AllowPlace,
    IReadOnlyList<string> HostSuffixes)
{
    public bool Matches(string host) =>
        HostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Chooses the <see cref="ServerProfile"/> for a host and applies it to Baritone's live settings.
///
/// Applied from the tick hook on host change rather than at each call site, because there are five places that
/// connect a client today and a sixth would otherwise silently get vanilla permissions on a server that
/// forbids them.
/// </summary>
public static class ServerProfiles
{
    /// <summary>
    /// Servers where building is impossible. Adding one is the whole configuration story: a host suffix and
    /// what it forbids.
    /// </summary>
    public static readonly IReadOnlyList<ServerProfile> Known =
    [
        new("hypixel", AllowBreak: false, AllowPlace: false, ["hypixel.net"])
    ];

    /// <summary>Vanilla behaviour, used for local servers and anything not listed.</summary>
    public static readonly ServerProfile Default = new("default", AllowBreak: true, AllowPlace: true, []);

    public static ServerProfile For(string? host) =>
        string.IsNullOrWhiteSpace(host)
            ? Default
            : Known.FirstOrDefault(p => p.Matches(host)) ?? Default;

    /// <summary>
    /// Pushes a profile into Baritone's settings. Idempotent, so the caller may apply it as often as it likes.
    /// </summary>
    public static void Apply(ServerProfile profile)
    {
        var settings = BaritoneSettings.Settings();
        settings.AllowBreak.Value = profile.AllowBreak;
        settings.AllowPlace.Value = profile.AllowPlace;
    }
}
