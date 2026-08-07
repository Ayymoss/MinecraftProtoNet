using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// Converts NBT into shapes that survive JSON serialisation, for reference capture.
///
/// Core's <see cref="MinecraftProtoNet.Core.Utilities.ItemTextHelper"/> deliberately flattens components and
/// strips § codes, which is right for display but lossy for a reference dump: server NPC names carry their
/// colour either as legacy § codes inside the text or as component fields, and both are worth keeping. So this
/// preserves the raw text verbatim AND retains the full component tree.
/// </summary>
public static class NbtDump
{
    /// <summary>
    /// Projects a tag into nested dictionaries/lists/primitives that System.Text.Json can serialise directly.
    /// </summary>
    public static object? ToPlain(NbtTag? tag) => tag switch
    {
        null => null,
        NbtCompound compound => compound.Value
            .GroupBy(t => t.Name ?? "")
            // Duplicate keys are not legal NBT, but a malformed server payload must not throw mid-capture.
            .ToDictionary(g => g.Key, g => g.Count() == 1 ? ToPlain(g.First()) : g.Select(ToPlain).ToList()),
        NbtList list => list.Value.Select(ToPlain).ToList(),
        NbtString s => s.Value,
        NbtByte b => b.Value,
        NbtShort s => s.Value,
        NbtInt i => i.Value,
        NbtLong l => l.Value,
        NbtFloat f => f.Value,
        NbtDouble d => d.Value,
        NbtByteArray ba => ba.Value,
        NbtIntArray ia => ia.Value,
        NbtLongArray la => la.Value,
        _ => tag.ToString()
    };

    /// <summary>
    /// Flattens a text component to a string, keeping § formatting codes exactly as the server sent them.
    /// </summary>
    public static string RawText(NbtTag? tag)
    {
        if (tag is null) return string.Empty;

        switch (tag)
        {
            case NbtString str:
                return str.Value;

            case NbtList list:
                return string.Concat(list.Value.Select(RawText));

            case NbtCompound compound:
            {
                var parts = new List<string>();

                if (compound.Value.FirstOrDefault(t => t.Name == "text") is NbtString text)
                {
                    parts.Add(text.Value);
                }

                // A translate key has no client-side resolution here; keep the key so the entry is still
                // identifiable rather than silently emitting an empty name.
                if (compound.Value.FirstOrDefault(t => t.Name == "translate") is NbtString translate)
                {
                    parts.Add($"[{translate.Value}]");
                }

                if (compound.Value.FirstOrDefault(t => t.Name == "extra") is NbtList extra)
                {
                    parts.AddRange(extra.Value.Select(RawText));
                }

                return string.Concat(parts);
            }

            default:
                return string.Empty;
        }
    }
}
