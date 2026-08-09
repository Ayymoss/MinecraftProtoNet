using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.State;

/// <summary>
/// The scoreboard sidebar as the player sees it, and the figures worth reading out of it.
///
/// Servers use the sidebar as their real HUD, and SkyBlock puts the player's coin balance there and nowhere
/// else — it is not in any packet field, so a bot that ignores the scoreboard cannot know what it can afford.
/// The rendering is indirect: each visible line is a score whose "owner" is an invisible marker string, and
/// the text a player actually reads is that owner's TEAM prefix and suffix. So a line is only reconstructable
/// by joining three packets — SetDisplayObjective (which objective is on screen), SetScore (which owners, and
/// in what order) and SetPlayerTeam (the text).
/// </summary>
public sealed class Sidebar
{
    private readonly ConcurrentDictionary<string, int> _scores = new(StringComparer.Ordinal);

    /// <summary>Objective currently displayed in the sidebar slot, if any.</summary>
    public string? ObjectiveName { get; private set; }

    public void SetDisplayedObjective(int position, string objectiveName)
    {
        // Slot 1 is the sidebar. 0 is the player list and 2 is below-name, neither of which is on screen.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/scores/DisplaySlot.java
        if (position != 1) return;

        if (!string.Equals(ObjectiveName, objectiveName, StringComparison.Ordinal))
        {
            ObjectiveName = objectiveName;
            _scores.Clear();
        }
    }

    public void SetScore(string owner, string objectiveName, int value)
    {
        if (ObjectiveName is null || !string.Equals(objectiveName, ObjectiveName, StringComparison.Ordinal)) return;
        _scores[owner] = value;
    }

    public void RemoveScore(string owner) => _scores.TryRemove(owner, out _);

    public void Clear()
    {
        ObjectiveName = null;
        _scores.Clear();
    }

    /// <summary>
    /// The visible lines, top to bottom. Minecraft draws the sidebar in descending score order.
    /// </summary>
    public List<string> Lines(TeamRegistry teams)
    {
        return _scores
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var team = teams.GetTeamOf(kv.Key);
                var text = $"{team?.Prefix ?? ""}{kv.Key}{team?.Suffix ?? ""}";
                return ItemTextHelper.StripFormattingCodes(text).Trim();
            })
            .Where(l => l.Length > 0)
            .ToList();
    }

    // Matches "Purse: 1,234,567" and SkyBlock's shorthand "Purse: 12.3M", with an optional trailing bonus
    // like "Purse: 1,000 (+500)" that must not be read as part of the balance.
    private static readonly Regex PurseLine = new(
        @"(?:purse|piggy)\s*:\s*([0-9][0-9,.]*)\s*([kmb])?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The player's coin balance, or null when the sidebar does not show one — a lobby, or a server that has
    /// no such concept. Null means "unknown", never "zero": treating it as zero would stop all trading.
    /// </summary>
    public double? ReadPurse(TeamRegistry teams)
    {
        foreach (var line in Lines(teams))
        {
            var match = PurseLine.Match(line);
            if (!match.Success) continue;

            var digits = match.Groups[1].Value.Replace(",", "");
            if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;

            return match.Groups[2].Value.ToLowerInvariant() switch
            {
                "k" => value * 1_000,
                "m" => value * 1_000_000,
                "b" => value * 1_000_000_000,
                _ => value
            };
        }

        return null;
    }
}
