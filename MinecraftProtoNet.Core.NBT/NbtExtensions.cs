using System.Text;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;

namespace MinecraftProtoNet.Core.NBT;

public static class NbtExtensions
{
    /// <summary>
    /// Finds all NBT tags with the specified name and type within the given NBT tag.
    /// </summary>
    /// <typeparam name="T">The expected type of the NBT tag (e.g., NbtString, NbtInt).</typeparam>
    /// <param name="rootTag">The root NBT tag to search within.</param>
    /// <param name="tagName">The name of the tag to search for.</param>
    /// <returns>An IEnumerable of matching NBT tags.  Returns an empty enumerable if no matches are found.</returns>
    public static IEnumerable<T> FindTags<T>(this NbtTag? rootTag, string? tagName) where T : NbtTag
    {
        if (rootTag is null) yield break;

        Stack<NbtTag> stack = new();
        stack.Push(rootTag);

        while (stack.Count > 0)
        {
            var currentTag = stack.Pop();

            if (currentTag.Name == tagName && currentTag is T typedTag)
            {
                yield return typedTag;
            }

            switch (currentTag)
            {
                case NbtCompound compoundTag:
                {
                    foreach (var childTag in compoundTag.Value)
                    {
                        stack.Push(childTag);
                    }

                    break;
                }
                case NbtList listTag:
                {
                    foreach (var listItem in listTag.Value)
                    {
                        stack.Push(listItem);
                    }

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Finds the first NBT tag with the specified name and type within the given NBT tag.
    /// </summary>
    /// <typeparam name="T">The type of the NBT tag.</typeparam>
    /// <param name="rootTag">The root NBT tag to search.</param>
    /// <param name="tagName">The name of the tag.</param>
    /// <returns>The first matching tag, or null if not found.</returns>
    public static T? FindTag<T>(this NbtTag? rootTag, string? tagName) where T : NbtTag
    {
        return rootTag.FindTags<T>(tagName).FirstOrDefault();
    }

    /// <summary>
    /// Finds an NBT tag by its path and type.
    /// </summary>
    /// <typeparam name="T">The expected type of the NBT tag.</typeparam>
    /// <param name="rootTag">The root NBT tag to search within.</param>
    /// <param name="path">The path to the tag (e.g., "root.child.grandchild").</param>
    /// <returns>The matching NBT tag, or null if not found.</returns>
    public static T? FindTagByPath<T>(this NbtTag? rootTag, string path) where T : NbtTag
    {
        if (rootTag is null) return null;

        var pathParts = path.Split('.');
        var currentTag = rootTag;

        foreach (var part in pathParts)
        {
            if (currentTag is not NbtCompound compoundTag) return null;

            var nextTag = compoundTag.Value.FirstOrDefault(t => t.Name == part);
            if (nextTag is null) return null;

            currentTag = nextTag;
        }

        return currentTag as T;
    }

    /// <summary>
    /// Converts an NBT tag tree to a human-readable SNBT-style string for logging.
    /// </summary>
    public static string ToSnbt(this NbtTag? tag, int maxDepth = 6)
    {
        if (tag is null) return "null";
        var sb = new StringBuilder();
        AppendSnbt(sb, tag, 0, maxDepth);
        return sb.ToString();
    }

    private static void AppendSnbt(StringBuilder sb, NbtTag tag, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            sb.Append("...");
            return;
        }

        switch (tag)
        {
            case NbtCompound compound:
                sb.Append('{');
                var first = true;
                foreach (var child in compound.Value)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    if (child.Name is not null)
                    {
                        sb.Append(child.Name);
                        sb.Append(": ");
                    }
                    AppendSnbt(sb, child, depth + 1, maxDepth);
                }
                sb.Append('}');
                break;

            case NbtList list:
                sb.Append('[');
                for (var i = 0; i < list.Value.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    if (i >= 8)
                    {
                        sb.Append($"...+{list.Value.Count - i}");
                        break;
                    }
                    AppendSnbt(sb, list.Value[i], depth + 1, maxDepth);
                }
                sb.Append(']');
                break;

            case NbtString s:
                // Show exact bytes for debugging string corruption
                sb.Append('"');
                foreach (var c in s.Value)
                {
                    if (c < 0x20 || c > 0x7E)
                        sb.Append($"\\x{(int)c:X2}");
                    else
                        sb.Append(c);
                }
                sb.Append('"');
                break;

            case NbtByte b:
                sb.Append(b.Value);
                sb.Append('b');
                break;
            case NbtShort sh:
                sb.Append(sh.Value);
                sb.Append('s');
                break;
            case NbtInt i:
                sb.Append(i.Value);
                break;
            case NbtLong l:
                sb.Append(l.Value);
                sb.Append('L');
                break;
            case NbtFloat f:
                sb.Append(f.Value);
                sb.Append('f');
                break;
            case NbtDouble d:
                sb.Append(d.Value);
                sb.Append('d');
                break;
            case NbtByteArray ba:
                sb.Append($"[B;{ba.Value.Length} bytes]");
                break;
            case NbtIntArray ia:
                sb.Append($"[I;{ia.Value.Length} ints]");
                break;
            case NbtLongArray la:
                sb.Append($"[L;{la.Value.Length} longs]");
                break;
            default:
                sb.Append(tag.ToString());
                break;
        }
    }
}
