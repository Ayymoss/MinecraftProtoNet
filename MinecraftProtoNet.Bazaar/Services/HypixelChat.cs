using System.Text.RegularExpressions;

namespace MinecraftProtoNet.Bazaar.Services;

/// <summary>
/// Tells apart what the SERVER said from what another PLAYER said.
///
/// Hypixel does not use signed player chat. Lobby chat is rendered server-side and delivered as a System Chat
/// packet — the same packet its own notices arrive on — so both land in the same handler and the packet type
/// cannot separate them. The only reliable difference left in the stripped text is the prefix: a player line
/// always carries a name ("Name: ..."), optionally behind rank tags ("[MVP+] Name: ...") and/or a channel
/// ("Guild > [VIP] Name: ..."). Hypixel's own notices begin with the notice itself.
///
/// This matters because the bot ACTS on some notices. "This server is too laggy to use the Bazaar, sorry!"
/// moves it to another hub — and "bruh server is to laggy" is a thing players type in lobby chat, which is in
/// our own capture logs. Without this check the second one moves the bot off a hub that was working fine.
/// </summary>
public static class HypixelChat
{
    private static readonly Regex PlayerPrefix = new(
        @"^(?:(?:Guild|Officer|Party|Co-op|Team|Friend|To|From)\s*>?\s*)?(?:\[[^\]]{1,24}\]\s*)*[A-Za-z0-9_]{3,16}\s*:\s",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// True when the line was written by another player. Expects text with formatting codes already stripped
    /// and the line trimmed, which is how it arrives from the chat event bus.
    ///
    /// Biased towards answering "player" on anything ambiguous. The two mistakes are not equal: treating a
    /// server notice as chat costs one missed hub hop, which the next notice repeats, while treating chat as a
    /// server notice hands strangers a way to push the bot around by typing at it.
    /// </summary>
    public static bool IsPlayerChat(string line) =>
        !string.IsNullOrWhiteSpace(line) && PlayerPrefix.IsMatch(line);
}
