using System.Text.RegularExpressions;

namespace PacketIdSync;

/// <summary>
/// Parses *Protocols.java files to extract packet registrations and their IDs.
/// Supports all protocol states: Game (Play), Configuration, Login, Status, Handshake.
/// </summary>
public static class JavaProtocolParser
{
    /// <summary>
    /// Represents a packet registration from a Java protocol file.
    /// </summary>
    public record PacketRegistration(
        string JavaTypeName,      // e.g., "ClientboundLoginPacket" or "ServerboundChatPacket"
        int PacketId,             // The computed ID based on registration order
        PacketDirection Direction,
        ProtocolState State
    );

    public enum PacketDirection
    {
        Clientbound,
        Serverbound
    }

    public enum ProtocolState
    {
        Play,
        Configuration,
        Login,
        Status,
        Handshaking
    }

    /// <summary>
    /// Maps a *Protocols.java filename to its protocol state.
    /// </summary>
    private static readonly Dictionary<string, ProtocolState> FileNameToState = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GameProtocols.java"] = ProtocolState.Play,
        ["ConfigurationProtocols.java"] = ProtocolState.Configuration,
        ["LoginProtocols.java"] = ProtocolState.Login,
        ["StatusProtocols.java"] = ProtocolState.Status,
        ["HandshakeProtocols.java"] = ProtocolState.Handshaking,
    };

    /// <summary>
    /// Maps a protocol state to the expected C# packets subdirectory name.
    /// </summary>
    public static readonly Dictionary<ProtocolState, string> StateToCSharpDir = new()
    {
        [ProtocolState.Play] = "Play",
        [ProtocolState.Configuration] = "Configuration",
        [ProtocolState.Login] = "Login",
        [ProtocolState.Status] = "Status",
        [ProtocolState.Handshaking] = "Handshaking",
    };

    /// <summary>
    /// Infers the protocol state from a *Protocols.java filename.
    /// Returns null if the filename is not recognized.
    /// </summary>
    public static ProtocolState? InferStateFromFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return FileNameToState.TryGetValue(fileName, out var state) ? state : null;
    }

    /// <summary>
    /// Discovers all *Protocols.java files under a root directory.
    /// Returns a list of (filePath, protocolState) tuples.
    /// </summary>
    public static List<(string FilePath, ProtocolState State)> DiscoverProtocolFiles(string rootDirectory)
    {
        var results = new List<(string, ProtocolState)>();
        var javaFiles = Directory.GetFiles(rootDirectory, "*Protocols.java", SearchOption.AllDirectories);

        foreach (var file in javaFiles)
        {
            var state = InferStateFromFileName(file);
            if (state.HasValue)
            {
                results.Add((file, state.Value));
            }
        }

        return results;
    }

    /// <summary>
    /// Parses a *Protocols.java file and extracts all packet registrations.
    /// </summary>
    public static List<PacketRegistration> ParseProtocolFile(string javaFilePath, ProtocolState state)
    {
        if (!File.Exists(javaFilePath))
        {
            throw new FileNotFoundException($"Protocol file not found at: {javaFilePath}");
        }

        var content = File.ReadAllText(javaFilePath);
        var registrations = new List<PacketRegistration>();

        // Find section boundaries independently — don't assume ordering or that both exist
        var serverboundStart = content.IndexOf("SERVERBOUND_TEMPLATE =", StringComparison.Ordinal);
        var clientboundStart = content.IndexOf("CLIENTBOUND_TEMPLATE =", StringComparison.Ordinal);

        if (serverboundStart >= 0)
        {
            // Serverbound section ends at clientbound start (if it exists and comes after) or end of content
            var serverboundEnd = (clientboundStart > serverboundStart) ? clientboundStart : content.Length;
            var serverboundSection = content[serverboundStart..serverboundEnd];
            registrations.AddRange(ParseSection(serverboundSection, PacketDirection.Serverbound, state));
        }

        if (clientboundStart >= 0)
        {
            // Clientbound section ends at serverbound start (if it exists and comes after) or end of content
            var clientboundEnd = (serverboundStart > clientboundStart) ? serverboundStart : content.Length;
            var clientboundSection = content[clientboundStart..clientboundEnd];
            registrations.AddRange(ParseSection(clientboundSection, PacketDirection.Clientbound, state));
        }

        return registrations;
    }

    /// <summary>
    /// Backward-compatible overload — parses with Play state by default.
    /// </summary>
    public static List<PacketRegistration> ParseGameProtocols(string javaFilePath)
    {
        return ParseProtocolFile(javaFilePath, ProtocolState.Play);
    }

    private static List<PacketRegistration> ParseSection(string section, PacketDirection direction, ProtocolState state)
    {
        var registrations = new List<(int position, string className)>();

        // Normalize whitespace - collapse all whitespace to single spaces
        var normalizedSection = Regex.Replace(section, @"\s+", " ");

        // Pattern 1: Match .addPacket/.withBundlePacket with any *STREAM_CODEC variant
        // Handles: STREAM_CODEC, GAMEPLAY_STREAM_CODEC, CONFIG_STREAM_CODEC, CONTEXT_FREE_STREAM_CODEC
        var streamCodecPattern = @"\.(addPacket|withBundlePacket)\s*\([^,]+,\s*(\w+)(Packet)?(?:\.(\w+))?\.\w*STREAM_CODEC";

        foreach (Match match in Regex.Matches(normalizedSection, streamCodecPattern))
        {
            var baseName = match.Groups[2].Value;
            var hasPacketSuffix = match.Groups[3].Success;
            var nestedClass = match.Groups[4].Success ? match.Groups[4].Value : null;

            var className = BuildClassName(baseName, hasPacketSuffix, nestedClass);
            registrations.Add((match.Index, className));
        }

        // Pattern 2: Match withBundlePacket with ::new (for bundle delimiter)
        // .withBundlePacket(GamePacketTypes.CLIENTBOUND_BUNDLE, ClientboundBundlePacket::new, ...)
        var bundleNewPattern = @"\.withBundlePacket\s*\([^,]+,\s*(\w+)(Packet)?::new";

        foreach (Match match in Regex.Matches(normalizedSection, bundleNewPattern))
        {
            var baseName = match.Groups[1].Value;
            var hasPacketSuffix = match.Groups[2].Success;

            var className = BuildClassName(baseName, hasPacketSuffix, null);
            registrations.Add((match.Index, className));
        }

        // Sort by position in the file to get correct packet IDs
        var sortedRegistrations = registrations
            .OrderBy(r => r.position)
            .Select((r, idx) => new PacketRegistration(r.className, idx, direction, state))
            .ToList();

        return sortedRegistrations;
    }

    private static string BuildClassName(string baseName, bool hasPacketSuffix, string? nestedClass)
    {
        // If there's a nested class suffix, we need to insert it before "Packet"
        // e.g., MoveEntityPacket + Pos => MoveEntityPosPacket
        if (!string.IsNullOrEmpty(nestedClass) && !nestedClass.EndsWith("STREAM_CODEC"))
        {
            // If baseName already ends with "Packet", insert the nested class before it
            if (baseName.EndsWith("Packet"))
            {
                var baseWithoutPacket = baseName[..^"Packet".Length];
                return $"{baseWithoutPacket}{nestedClass}Packet";
            }
            else if (hasPacketSuffix)
            {
                // "Packet" was captured separately
                return $"{baseName}{nestedClass}Packet";
            }
            else
            {
                return $"{baseName}{nestedClass}Packet";
            }
        }

        // No nested class - just ensure it ends with Packet
        if (hasPacketSuffix)
        {
            return baseName + "Packet";
        }
        else if (!baseName.EndsWith("Packet"))
        {
            return baseName + "Packet";
        }

        return baseName;
    }
}
