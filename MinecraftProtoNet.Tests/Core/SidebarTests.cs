using FluentAssertions;
using MinecraftProtoNet.Core.State;
using Xunit;

namespace MinecraftProtoNet.Tests.Core;

/// <summary>
/// The sidebar is the only place SkyBlock publishes the player's coin balance, and it is published as
/// rendered text rather than a field — so the parsing is load-bearing for anything that decides what the bot
/// can afford, and wrong answers are silent.
/// </summary>
public class SidebarTests
{
    /// <summary>Builds a sidebar the way a server does: a displayed objective, scores, and team text.</summary>
    private static (Sidebar Sidebar, TeamRegistry Teams) Build(params (string Owner, int Score, string Prefix, string Suffix)[] lines)
    {
        var sidebar = new Sidebar();
        var teams = new TeamRegistry();
        sidebar.SetDisplayedObjective(1, "SBScoreboard");

        foreach (var (owner, score, prefix, suffix) in lines)
        {
            sidebar.SetScore(owner, "SBScoreboard", score);
            var team = teams.GetOrCreate($"team_{owner}");
            team.Prefix = prefix;
            team.Suffix = suffix;
            teams.AddMembers(team.Name, [owner]);
        }

        return (sidebar, teams);
    }

    /// <summary>
    /// Score owners are invisible markers made of formatting codes — SkyBlock needs each line to have a
    /// unique holder, but none of it may show — so the visible line is the team's prefix and suffix alone.
    /// </summary>
    [Fact]
    public void Lines_AreOrderedByDescendingScore()
    {
        var (sidebar, teams) = Build(
            ("§a", 1, "bottom", ""),
            ("§b", 3, "top", ""),
            ("§c", 2, "middle", ""));

        sidebar.Lines(teams).Should().Equal("top", "middle", "bottom");
    }

    [Theory]
    [InlineData("Purse: 1,234,567", 1234567)]
    [InlineData("Purse: 950", 950)]
    [InlineData("Piggy: 4,000", 4000)]
    [InlineData("Purse: 12.3M", 12_300_000)]
    [InlineData("Purse: 45K", 45_000)]
    public void ReadPurse_ParsesTheBalance(string line, double expected)
    {
        var (sidebar, teams) = Build(("x", 5, line, ""));

        sidebar.ReadPurse(teams).Should().Be(expected);
    }

    /// <summary>
    /// SkyBlock appends a transient bonus after a sale ("Purse: 1,000 (+500)"). Reading the bonus as part of
    /// the balance would overstate what the bot can spend, so only the first number counts.
    /// </summary>
    [Fact]
    public void ReadPurse_IgnoresATrailingBonus()
    {
        var (sidebar, teams) = Build(("x", 5, "Purse: 1,000 (+500)", ""));

        sidebar.ReadPurse(teams).Should().Be(1000);
    }

    /// <summary>The text is split across prefix and suffix as often as not.</summary>
    [Fact]
    public void ReadPurse_ReadsTextSplitAcrossPrefixAndSuffix()
    {
        var (sidebar, teams) = Build(("Purse:", 5, "", " 777,000"));

        sidebar.ReadPurse(teams).Should().Be(777_000);
    }

    [Fact]
    public void ReadPurse_IsNullWhenNoPurseLineExists()
    {
        var (sidebar, teams) = Build(("x", 5, "Bank: 100", ""));

        sidebar.ReadPurse(teams).Should().BeNull("unknown must never be reported as zero");
    }

    /// <summary>A new objective replaces the old sidebar; stale lines must not survive a world change.</summary>
    [Fact]
    public void SwitchingObjective_DropsPreviousScores()
    {
        var (sidebar, teams) = Build(("x", 5, "Purse: 500", ""));

        sidebar.SetDisplayedObjective(1, "Different");

        sidebar.Lines(teams).Should().BeEmpty();
        sidebar.ReadPurse(teams).Should().BeNull();
    }

    /// <summary>Slot 1 is the on-screen sidebar; the player list and below-name slots are not.</summary>
    [Fact]
    public void ScoresForNonSidebarSlots_AreIgnored()
    {
        var sidebar = new Sidebar();
        sidebar.SetDisplayedObjective(0, "PlayerList");

        sidebar.ObjectiveName.Should().BeNull();
    }
}
