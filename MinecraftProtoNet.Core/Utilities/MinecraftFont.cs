namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// Rendered width of text in vanilla's default font, in pixels at scale 1 — i.e. what `Font.width(String)`
/// returns in the Java client.
///
/// This exists because several client-side input limits are expressed in PIXELS, not characters, and a bot
/// that ignores them sends values a real client is physically incapable of producing. The sign editor is the
/// sharp case: `AbstractSignEditScreen` builds its text field with the filter
///     (s) -&gt; this.minecraft.font.width(s) &lt;= this.sign.getMaxTextLineWidth()
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/gui/screens/inventory/AbstractSignEditScreen.java:58
/// and the limit itself is `SignBlockEntity.MAX_TEXT_LINE_WIDTH = 90` (hanging signs: 60)
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/entity/SignBlockEntity.java:39
///
/// That filter runs per keystroke, so the line can never grow past the limit — typing simply stops having an
/// effect. "Enchanted Spruc" is 86px and fits; adding the "e" would make it 92px and is rejected outright.
/// A client that posts the full "Enchanted Spruce Log" (114px) is announcing that its text did not come from
/// the sign editor at all.
///
/// The widths are the vanilla ASCII advances (glyph width + 1px spacing). Anything outside the table falls
/// back to 6, the common case, which keeps an unexpected character from silently under-counting.
/// </summary>
public static class MinecraftFont
{
    /// <summary>Vanilla's per-line pixel budget for a standard sign.</summary>
    public const int SignMaxLineWidth = 90;

    /// <summary>Vanilla's per-line pixel budget for a hanging sign.</summary>
    public const int HangingSignMaxLineWidth = 60;

    private const int DefaultAdvance = 6;

    private static readonly Dictionary<char, int> Advances = new()
    {
        [' '] = 4, ['!'] = 2, ['"'] = 5, ['#'] = 6, ['$'] = 6, ['%'] = 6, ['&'] = 6, ['\''] = 3,
        ['('] = 5, [')'] = 5, ['*'] = 5, ['+'] = 6, [','] = 2, ['-'] = 6, ['.'] = 2, ['/'] = 6,
        [':'] = 2, [';'] = 2, ['<'] = 5, ['='] = 6, ['>'] = 5, ['?'] = 6, ['@'] = 7,
        ['I'] = 4,
        ['['] = 4, ['\\'] = 6, [']'] = 4, ['^'] = 6, ['_'] = 6, ['`'] = 3,
        ['f'] = 5, ['i'] = 2, ['k'] = 5, ['l'] = 3, ['t'] = 4,
        ['{'] = 5, ['|'] = 2, ['}'] = 5, ['~'] = 7
    };

    /// <summary>Rendered width of a single character, in pixels.</summary>
    public static int Width(char c) => Advances.TryGetValue(c, out var w) ? w : DefaultAdvance;

    /// <summary>Rendered width of a string, in pixels — vanilla's Font.width(String).</summary>
    public static int Width(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var total = 0;
        foreach (var c in text) total += Width(c);
        return total;
    }

    /// <summary>
    /// The value to put on a sign line WE are typing — clamped to what the editor's per-keystroke filter
    /// would have allowed a person to enter.
    ///
    /// Use this at the point text is authored, never on a whole packet: vanilla only filters the line being
    /// edited, and echoes lines it did not touch back verbatim. Clamping the untouched ones would rewrite
    /// text the server put there, which is a divergence of its own.
    /// </summary>
    public static string TypedSignLine(string? text, int maxWidth = SignMaxLineWidth) =>
        TruncateToWidth(text, maxWidth);

    /// <summary>
    /// Trims text to what would actually fit in a sign line, exactly as the editor's per-keystroke filter
    /// would have. Returns the input unchanged when it already fits, so the common case costs nothing.
    /// </summary>
    public static string TruncateToWidth(string? text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (Width(text) <= maxWidth) return text;

        var total = 0;
        for (var i = 0; i < text.Length; i++)
        {
            total += Width(text[i]);
            if (total > maxWidth) return text[..i];
        }

        return text;
    }
}
