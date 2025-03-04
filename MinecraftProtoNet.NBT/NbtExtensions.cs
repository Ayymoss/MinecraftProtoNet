using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.NBT.Tags.Abstract;

namespace MinecraftProtoNet.NBT;

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
}
