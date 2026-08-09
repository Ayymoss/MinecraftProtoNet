using FluentAssertions;
using MinecraftProtoNet.Core.Utilities;
using Xunit;

namespace MinecraftProtoNet.Tests.Core;

/// <summary>
/// The sign editor limits a line by RENDERED PIXEL WIDTH, not character count, and it does so per keystroke —
/// so a real client can never post a longer line. These tests pin the width table against a case observed in
/// the actual game client: typing "Enchanted Spruce Log" into a Bazaar search sign stops at "Enchanted Spruc".
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/gui/screens/inventory/AbstractSignEditScreen.java:58
///            minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/entity/SignBlockEntity.java:39
/// </summary>
public class MinecraftFontTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData(" ", 4)]
    [InlineData("i", 2)]
    [InlineData("l", 3)]
    [InlineData("t", 4)]
    [InlineData("I", 4)]
    [InlineData("a", 6)]
    [InlineData("@", 7)]
    public void Width_ShouldMatchVanillaAdvances(string text, int expected) =>
        MinecraftFont.Width(text).Should().Be(expected);

    [Fact]
    public void Width_ShouldSumGlyphAdvances() =>
        // E6 n6 c6 h6 a6 n6 t4 e6 d6 = 52, space 4 = 56, S6 p6 r6 u6 c6 e6 = 92, space 4 = 96, L6 o6 g6 = 114
        MinecraftFont.Width("Enchanted Spruce Log").Should().Be(114);

    [Fact]
    public void TruncateToWidth_ShouldStopWhereTheGameClientStops()
    {
        // Observed in-game: the editor accepts "Enchanted Spruc" (86px) and refuses the next character,
        // because it would take the line to 92px — past the 90px budget.
        MinecraftFont.Width("Enchanted Spruc").Should().Be(86);
        MinecraftFont.Width("Enchanted Spruce").Should().Be(92);

        MinecraftFont.TruncateToWidth("Enchanted Spruce Log", MinecraftFont.SignMaxLineWidth)
            .Should().Be("Enchanted Spruc");
    }

    [Theory]
    // Things the bot types that already fit are passed through untouched — the fix must not mangle prices.
    [InlineData("1940.8")]
    [InlineData("36122")]
    [InlineData("Solar Shard")]
    [InlineData("Jacob's Ticket")]
    [InlineData("Cavernfish Shard")] // 89px — one pixel inside the limit
    public void TruncateToWidth_ShouldLeaveFittingTextAlone(string text) =>
        MinecraftFont.TruncateToWidth(text, MinecraftFont.SignMaxLineWidth).Should().Be(text);

    [Fact]
    public void TruncateToWidth_ShouldNeverExceedTheBudget()
    {
        foreach (var text in new[]
                 {
                     "Enchanted Spruce Log", "Enchanted Lapis Lazuli Block", "Hypergolic Gabagool",
                     "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii", "WWWWWWWWWWWWWWWWWWWW"
                 })
        {
            var clipped = MinecraftFont.TruncateToWidth(text, MinecraftFont.SignMaxLineWidth);
            MinecraftFont.Width(clipped).Should().BeLessThanOrEqualTo(MinecraftFont.SignMaxLineWidth);
        }
    }

    [Fact]
    public void TruncateToWidth_ShouldHandleNullAndEmpty()
    {
        MinecraftFont.TruncateToWidth(null, MinecraftFont.SignMaxLineWidth).Should().BeEmpty();
        MinecraftFont.TruncateToWidth("", MinecraftFont.SignMaxLineWidth).Should().BeEmpty();
    }

    [Fact]
    public void HangingSigns_ShouldUseTheTighterBudget() =>
        MinecraftFont.HangingSignMaxLineWidth.Should().Be(60);
}
