using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// One resolved piece of styled text from a Minecraft text component.
/// <see cref="Color"/> can be a named colour (e.g. "green"), a 6-digit hex string ("#FFAA00"),
/// or null when the component did not set one (the renderer should apply its default).
/// </summary>
public record MinecraftTextSpan(
    string Text,
    string? Color,
    bool Bold,
    bool Italic,
    bool Underlined,
    bool Strikethrough,
    bool Obfuscated);

/// <summary>
/// Internal mutable style carried while walking the tree. NBT-form and legacy-§ form are merged here
/// because the game mixes both (especially on vanilla-flavoured public servers).
/// </summary>
internal record struct MinecraftTextStyle(
    string? Color,
    bool Bold,
    bool Italic,
    bool Underlined,
    bool Strikethrough,
    bool Obfuscated)
{
    public static MinecraftTextStyle Default => new(null, false, false, false, false, false);
}

/// <summary>
/// Flattens an NBT text component tree into a list of styled spans.
///
/// Handles:
/// - <c>text</c> leaves (with embedded §-codes expanded into separate spans);
/// - <c>translate</c> leaves (rendered as <c>[translate]</c> placeholder — Bazaar menus rarely use translate);
/// - <c>extra</c> lists of child components that inherit the parent style unless they override it;
/// - style fields: <c>color</c>, <c>bold</c>, <c>italic</c>, <c>underlined</c>, <c>strikethrough</c>, <c>obfuscated</c>;
/// - both string booleans ("true"/"false") and byte booleans (1b/0b), since servers use both.
///
/// Vanilla rule matched: in modern components, lore lines default <c>italic=false</c>. If a parent
/// explicitly sets italic=false, children inherit that — we don't force-disable italic here.
/// </summary>
public static class MinecraftTextParser
{
    public static List<MinecraftTextSpan> Parse(NbtTag? tag)
    {
        var result = new List<MinecraftTextSpan>();
        if (tag is null) return result;
        AppendSpans(result, tag, MinecraftTextStyle.Default);
        return result;
    }

    private static void AppendSpans(List<MinecraftTextSpan> output, NbtTag tag, MinecraftTextStyle parent)
    {
        switch (tag)
        {
            case NbtString s:
                AppendWithLegacyCodes(output, s.Value, parent);
                break;

            case NbtList list:
                // Lore is a list of components; each entry is independent and inherits default (not parent).
                foreach (var child in list.Value)
                {
                    AppendSpans(output, child, MinecraftTextStyle.Default);
                }
                break;

            case NbtCompound compound:
                AppendCompound(output, compound, parent);
                break;
        }
    }

    private static void AppendCompound(List<MinecraftTextSpan> output, NbtCompound compound, MinecraftTextStyle parent)
    {
        // Merge parent style with any overrides on this node.
        var style = parent with { };

        if (TryReadString(compound, "color", out var color) && !string.IsNullOrEmpty(color))
            style.Color = color;
        if (TryReadBool(compound, "bold", out var bold)) style.Bold = bold;
        if (TryReadBool(compound, "italic", out var italic)) style.Italic = italic;
        if (TryReadBool(compound, "underlined", out var underlined)) style.Underlined = underlined;
        if (TryReadBool(compound, "strikethrough", out var strikethrough)) style.Strikethrough = strikethrough;
        if (TryReadBool(compound, "obfuscated", out var obfuscated)) style.Obfuscated = obfuscated;

        // Self text.
        if (TryReadString(compound, "text", out var text) && !string.IsNullOrEmpty(text))
        {
            AppendWithLegacyCodes(output, text, style);
        }

        // Translate placeholder (we don't resolve language files here).
        if (TryReadString(compound, "translate", out var translate) && !string.IsNullOrEmpty(translate))
        {
            output.Add(new MinecraftTextSpan(
                Text: $"[{translate}]",
                Color: style.Color,
                Bold: style.Bold,
                Italic: style.Italic,
                Underlined: style.Underlined,
                Strikethrough: style.Strikethrough,
                Obfuscated: style.Obfuscated));
        }

        // Children inherit current style unless they override.
        if (compound.Value.FirstOrDefault(t => t.Name == "extra") is NbtList extraList)
        {
            foreach (var child in extraList.Value)
            {
                AppendSpans(output, child, style);
            }
        }
    }

    private static bool TryReadString(NbtCompound compound, string key, out string? value)
    {
        var tag = compound.Value.FirstOrDefault(t => t.Name == key);
        if (tag is NbtString s) { value = s.Value; return true; }
        value = null;
        return false;
    }

    private static bool TryReadBool(NbtCompound compound, string key, out bool value)
    {
        var tag = compound.Value.FirstOrDefault(t => t.Name == key);
        switch (tag)
        {
            case NbtByte b: value = b.Value != 0; return true;
            case NbtString s when bool.TryParse(s.Value, out var parsed): value = parsed; return true;
            default: value = false; return false;
        }
    }

    // Legacy §-code expansion — servers routinely mix §-codes into `text` fields even when they
    // also set modern NBT style fields. We split the raw string into spans so each §-code produces
    // its own styled span.
    //
    // Reference for colour table:
    // minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/ChatFormatting.java
    private static void AppendWithLegacyCodes(List<MinecraftTextSpan> output, string raw, MinecraftTextStyle baseStyle)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        var style = baseStyle;
        var buffer = new System.Text.StringBuilder(raw.Length);

        void Flush()
        {
            if (buffer.Length == 0) return;
            output.Add(new MinecraftTextSpan(
                buffer.ToString(), style.Color, style.Bold, style.Italic,
                style.Underlined, style.Strikethrough, style.Obfuscated));
            buffer.Clear();
        }

        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\u00A7' && i + 1 < raw.Length)
            {
                Flush();
                ApplyLegacyCode(ref style, baseStyle, raw[i + 1]);
                i++;
                continue;
            }
            buffer.Append(raw[i]);
        }
        Flush();
    }

    private static void ApplyLegacyCode(ref MinecraftTextStyle style, MinecraftTextStyle baseStyle, char code)
    {
        switch (char.ToLowerInvariant(code))
        {
            case '0': style.Color = "black"; break;
            case '1': style.Color = "dark_blue"; break;
            case '2': style.Color = "dark_green"; break;
            case '3': style.Color = "dark_aqua"; break;
            case '4': style.Color = "dark_red"; break;
            case '5': style.Color = "dark_purple"; break;
            case '6': style.Color = "gold"; break;
            case '7': style.Color = "gray"; break;
            case '8': style.Color = "dark_gray"; break;
            case '9': style.Color = "blue"; break;
            case 'a': style.Color = "green"; break;
            case 'b': style.Color = "aqua"; break;
            case 'c': style.Color = "red"; break;
            case 'd': style.Color = "light_purple"; break;
            case 'e': style.Color = "yellow"; break;
            case 'f': style.Color = "white"; break;
            case 'l': style.Bold = true; break;
            case 'm': style.Strikethrough = true; break;
            case 'n': style.Underlined = true; break;
            case 'o': style.Italic = true; break;
            case 'k': style.Obfuscated = true; break;
            case 'r': style = baseStyle; break;
        }
    }

    /// <summary>
    /// Named-colour → hex map matching vanilla's ChatFormatting palette. Unknown / hex colours
    /// should be returned verbatim; null means "use renderer default".
    /// </summary>
    public static string? ResolveColor(string? color)
    {
        if (string.IsNullOrEmpty(color)) return null;
        if (color[0] == '#') return color;
        return color.ToLowerInvariant() switch
        {
            "black"         => "#000000",
            "dark_blue"     => "#0000AA",
            "dark_green"    => "#00AA00",
            "dark_aqua"     => "#00AAAA",
            "dark_red"      => "#AA0000",
            "dark_purple"   => "#AA00AA",
            "gold"          => "#FFAA00",
            "gray"          => "#AAAAAA",
            "dark_gray"     => "#555555",
            "blue"          => "#5555FF",
            "green"         => "#55FF55",
            "aqua"          => "#55FFFF",
            "red"           => "#FF5555",
            "light_purple"  => "#FF55FF",
            "yellow"        => "#FFFF55",
            "white"         => "#FFFFFF",
            _               => null
        };
    }
}
