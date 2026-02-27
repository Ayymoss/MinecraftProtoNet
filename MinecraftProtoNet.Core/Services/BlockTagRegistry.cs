using System.Collections.Frozen;
using System.Text.Json;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Loads Minecraft block tags from datagen JSON files and provides fast tag membership lookups.
/// Tags are loaded from StaticFiles/data/minecraft/tags/block/**/*.json (including subdirectories).
/// Subdirectory tags use forward-slash names, e.g. "mineable/pickaxe".
/// Nested tag references (#minecraft:xxx) are resolved recursively.
/// </summary>
public class BlockTagRegistry
{
    private FrozenDictionary<string, FrozenSet<string>> _tags = FrozenDictionary<string, FrozenSet<string>>.Empty;
    private bool _initialized;

    private static readonly string TagsPath = Path.Combine(
        AppContext.BaseDirectory, "StaticFiles", "data", "minecraft", "tags", "block");

    /// <summary>
    /// Checks if a block is a member of a tag.
    /// </summary>
    /// <param name="blockName">Full block name, e.g. "minecraft:ladder"</param>
    /// <param name="tagName">Tag name without namespace prefix, e.g. "climbable"</param>
    public bool HasTag(string blockName, string tagName)
    {
        if (!_initialized) return false;
        return _tags.TryGetValue(tagName, out var members) && members.Contains(blockName);
    }

    /// <summary>
    /// Gets all block names in a tag. Returns empty set if tag not found.
    /// </summary>
    public IReadOnlySet<string> GetTag(string tagName)
    {
        if (!_initialized) return FrozenSet<string>.Empty;
        return _tags.TryGetValue(tagName, out var members) ? members : FrozenSet<string>.Empty;
    }

    /// <summary>
    /// Loads all block tag files from the StaticFiles directory.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        if (!Directory.Exists(TagsPath))
        {
            _initialized = true;
            return;
        }

        // Phase 1: Load raw tag data (values may contain #minecraft:xxx references)
        // Recursively scan subdirectories so tags like mineable/pickaxe are included.
        var rawTags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(TagsPath, "*.json", SearchOption.AllDirectories))
        {
            // Compute tag name from relative path: e.g. "climbable" or "mineable/pickaxe"
            var relativePath = Path.GetRelativePath(TagsPath, file);
            var tagName = Path.ChangeExtension(relativePath, null).Replace('\\', '/');
            try
            {
                var json = File.ReadAllText(file);
                var tagData = JsonSerializer.Deserialize<TagFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (tagData?.Values != null)
                {
                    rawTags[tagName] = tagData.Values;
                }
            }
            catch
            {
                // Skip malformed tag files
            }
        }

        // Phase 2: Resolve nested tag references recursively
        var resolved = new Dictionary<string, FrozenSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tagName in rawTags.Keys)
        {
            ResolveTag(tagName, rawTags, resolved, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        _tags = resolved.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _initialized = true;
    }

    private static FrozenSet<string> ResolveTag(
        string tagName,
        Dictionary<string, List<string>> rawTags,
        Dictionary<string, FrozenSet<string>> resolved,
        HashSet<string> resolving)
    {
        if (resolved.TryGetValue(tagName, out var existing))
            return existing;

        if (!rawTags.TryGetValue(tagName, out var rawValues))
        {
            var empty = FrozenSet<string>.Empty;
            resolved[tagName] = empty;
            return empty;
        }

        // Detect circular references
        if (!resolving.Add(tagName))
        {
            var empty = FrozenSet<string>.Empty;
            resolved[tagName] = empty;
            return empty;
        }

        var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in rawValues)
        {
            if (value.StartsWith('#'))
            {
                // Nested tag reference: #minecraft:wooden_slabs -> resolve "wooden_slabs"
                var referencedTag = value[1..]; // Remove #
                // Strip "minecraft:" prefix if present to get the tag file name
                if (referencedTag.StartsWith("minecraft:", StringComparison.OrdinalIgnoreCase))
                    referencedTag = referencedTag["minecraft:".Length..];

                var nestedMembers = ResolveTag(referencedTag, rawTags, resolved, resolving);
                foreach (var member in nestedMembers)
                    members.Add(member);
            }
            else
            {
                members.Add(value);
            }
        }

        resolving.Remove(tagName);
        var frozenMembers = members.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        resolved[tagName] = frozenMembers;
        return frozenMembers;
    }

    private record TagFile(List<string> Values);
}
