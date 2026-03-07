using MinecraftProtoNet.Core.NBT;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;
using MinecraftProtoNet.Core.Packets.Base.Definitions;

namespace MinecraftProtoNet.Bazaar.Gui;

/// <summary>
/// Reads item names and lore from Slot components for Bazaar GUI navigation.
/// Hypixel uses ComponentType.ItemName (type 9, NBT tag) for display names
/// and ComponentType.Lore (type 11, NBT tag list) for description lines.
/// </summary>
public static class BazaarGuiReader
{
    /// <summary>
    /// Gets the display name of an item from its CustomName or ItemName component.
    /// Returns null if the slot is empty or has no naming components.
    /// </summary>
    public static string? GetItemName(Slot? slot)
    {
        if (slot is null || slot.IsEmpty || slot.ComponentsToAdd is null)
            return null;

        // Prioritize CustomName (ID 6) for renamed/UI items, fallback to ItemName (ID 9) for base types
        var nameComponent = slot.ComponentsToAdd.FirstOrDefault(c => c.Type == ComponentType.CustomName)
                         ?? slot.ComponentsToAdd.FirstOrDefault(c => c.Type == ComponentType.ItemName);

        if (nameComponent?.Data is not NbtTag tag)
            return null;

        // Extract all text parts recursively from the NBT component
        var texts = tag.FindTags<NbtString>("text");
        var result = string.Join("", texts.Select(t => t.Value));
        if (!string.IsNullOrEmpty(result))
            return result;

        // Fallback: check for translate key
        var translate = tag.FindTag<NbtString>("translate");
        return translate?.Value;
    }

    /// <summary>
    /// Gets the lore lines of an item from its Lore component.
    /// Returns empty list if the slot has no lore.
    /// </summary>
    public static List<string> GetLoreLines(Slot? slot)
    {
        if (slot is null || slot.IsEmpty || slot.ComponentsToAdd is null)
            return [];

        foreach (var component in slot.ComponentsToAdd)
        {
            if (component.Type != ComponentType.Lore || component.Data is not object?[] loreTags)
                continue;

            var lines = new List<string>(loreTags.Length);
            foreach (var loreTag in loreTags)
            {
                if (loreTag is not NbtTag tag)
                    continue;

                var texts = tag.FindTags<NbtString>("text");
                var line = string.Join("", texts.Select(t => t.Value));
                lines.Add(line);
            }

            return lines;
        }

        return [];
    }

    /// <summary>
    /// Finds the first slot index whose item name contains the given substring (case-insensitive).
    /// Returns -1 if no match found.
    /// </summary>
    public static short FindSlotByName(Dictionary<short, Slot> slots, string substring)
    {
        foreach (var (index, slot) in slots)
        {
            var name = GetItemName(slot);
            if (name is not null && name.Contains(substring, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Finds the first slot index that has a lore line containing the given substring (case-insensitive).
    /// Returns -1 if no match found.
    /// </summary>
    public static short FindSlotByLore(Dictionary<short, Slot> slots, string substring)
    {
        foreach (var (index, slot) in slots)
        {
            var lore = GetLoreLines(slot);
            if (lore.Any(line => line.Contains(substring, StringComparison.OrdinalIgnoreCase)))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Gets all slot indices whose item names contain the given substring.
    /// </summary>
    public static List<short> FindAllSlotsByName(Dictionary<short, Slot> slots, string substring)
    {
        var results = new List<short>();
        foreach (var (index, slot) in slots)
        {
            var name = GetItemName(slot);
            if (name is not null && name.Contains(substring, StringComparison.OrdinalIgnoreCase))
                results.Add(index);
        }

        return results;
    }
}
