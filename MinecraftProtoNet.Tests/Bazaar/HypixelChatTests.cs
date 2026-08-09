using FluentAssertions;
using MinecraftProtoNet.Bazaar.Services;
using Xunit;

namespace MinecraftProtoNet.Tests.Bazaar;

/// <summary>
/// The bot moves hubs when the server says the Bazaar is unusable. Hypixel delivers lobby chat on the same
/// System Chat packet as its own notices, so if this rule is wrong a stranger can push the bot around by
/// typing at it — "bruh server is to laggy" appears verbatim in our capture logs.
///
/// The server strings here are copied from real captures (2026-08-09).
/// </summary>
public class HypixelChatTests
{
    [Theory]
    // The notice that started this: red System Chat, no name prefix.
    [InlineData("This server is too laggy to use the Bazaar, sorry!")]
    [InlineData("This server will restart soon!")]
    [InlineData("Evacuating to Your Island...")]
    [InlineData("You are sending commands too fast! Please slow down.")]
    // Centred event spam still has no name prefix once trimmed.
    [InlineData("HORSEMAN HORSE DOWN!")]
    public void ServerNotices_AreNotPlayerChat(string line) =>
        HypixelChat.IsPlayerChat(line).Should().BeFalse();

    [Theory]
    [InlineData("Ayymoss: bruh server is to laggy")]
    [InlineData("[MVP+] Ayymoss: bruh server is to laggy")]
    [InlineData("[MVP++] Notch: this server will restart soon lol")]
    [InlineData("Guild > [VIP] Someone: bazaar is disabled here")]
    [InlineData("Party > [MVP+] Someone: too laggy to use the bazaar")]
    [InlineData("Co-op > Player123: server is unavailable")]
    [InlineData("From [MVP+] Stranger: are you a bot")]
    public void PlayerChat_IsRecognised(string line) =>
        HypixelChat.IsPlayerChat(line).Should().BeTrue();

    [Fact]
    public void BlankLines_AreNotPlayerChat() =>
        HypixelChat.IsPlayerChat("   ").Should().BeFalse();

    /// <summary>
    /// The pairing that motivated the rule: identical claim, opposite handling, decided only by the prefix.
    /// </summary>
    [Fact]
    public void TheSameClaim_IsActedOnFromTheServerAndIgnoredFromAPlayer()
    {
        const string notice = "This server is too laggy to use the Bazaar, sorry!";
        HypixelChat.IsPlayerChat(notice).Should().BeFalse();
        HypixelChat.IsPlayerChat($"[MVP+] Someone: {notice}").Should().BeTrue();
    }
}
