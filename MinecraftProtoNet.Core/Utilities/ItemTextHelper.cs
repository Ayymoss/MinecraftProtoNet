using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;
using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// Helper for extracting and formatting item text from NBT components.
/// </summary>
public static class ItemTextHelper
{
    /// <summary>
    /// Gets the custom name from a slot's CustomName component.
    /// </summary>
    public static string? GetCustomName(Slot slot)
    {
        var customNameComponent = slot.ComponentsToAdd?
            .FirstOrDefault(c => c.Type == ComponentType.CustomName);

        if (customNameComponent?.Data is NbtTag nbt)
        {
            return FormatTextComponent(nbt);
        }

        return null;
    }

    /// <summary>
    /// Gets the item display name from the ItemName component.
    /// </summary>
    public static string? GetItemName(Slot slot)
    {
        var itemNameComponent = slot.ComponentsToAdd?
            .FirstOrDefault(c => c.Type == ComponentType.ItemName);

        if (itemNameComponent?.Data is NbtTag nbt)
        {
            return FormatTextComponent(nbt);
        }

        return null;
    }

    /// <summary>
    /// Gets the lore lines from a slot's Lore component.
    /// </summary>
    public static List<string> GetLore(Slot slot)
    {
        var loreComponent = slot.ComponentsToAdd?
            .FirstOrDefault(c => c.Type == ComponentType.Lore);

        var loreLines = new List<string>();

        if (loreComponent?.Data is NbtList list)
        {
            foreach (var item in list.Value)
            {
                var text = FormatTextComponent(item);
                if (!string.IsNullOrEmpty(text))
                {
                    loreLines.Add(text);
                }
            }
        }

        return loreLines;
    }

    /// <summary>
    /// Formats an NBT text component into a readable string.
    /// Handles both plain strings and JSON-style text components.
    /// </summary>
    public static string FormatTextComponent(NbtTag? tag)
    {
        if (tag is null) return string.Empty;

        return tag switch
        {
            NbtString str => StripFormattingCodes(str.Value),
            NbtCompound compound => FormatCompoundTextComponent(compound),
            _ => tag.ToString() ?? string.Empty
        };
    }

    private static string FormatCompoundTextComponent(NbtCompound compound)
    {
        var result = new List<string>();

        // Get the main text - search Value list for tag with name "text"
        var textTag = compound.Value.FirstOrDefault(t => t.Name == "text");
        if (textTag is NbtString text)
        {
            result.Add(StripFormattingCodes(text.Value));
        }

        // Check for translate key
        var translateTag = compound.Value.FirstOrDefault(t => t.Name == "translate");
        if (translateTag is NbtString translate)
        {
            result.Add($"[{translate.Value}]");
        }

        // Process "extra" array for additional text components
        var extraTag = compound.Value.FirstOrDefault(t => t.Name == "extra");
        if (extraTag is NbtList extra)
        {
            foreach (var item in extra.Value)
            {
                var formatted = FormatTextComponent(item);
                if (!string.IsNullOrEmpty(formatted))
                {
                    result.Add(formatted);
                }
            }
        }

        return string.Join("", result);
    }

    /// <summary>
    /// Strips Minecraft formatting codes (§ followed by a character).
    /// </summary>
    public static string StripFormattingCodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] == '§' && i + 1 < text.Length)
            {
                // Skip the formatting code character
                i += 2;
            }
            else
            {
                result.Append(text[i]);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the display name for an item (custom name or item name or null).
    /// </summary>
    public static string? GetDisplayName(Slot slot)
    {
        return GetCustomName(slot) ?? GetItemName(slot);
    }
}
