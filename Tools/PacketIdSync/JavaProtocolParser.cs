using System.Text.RegularExpressions;

namespace PacketIdSync;

/// <summary>
/// Parses GameProtocols.java to extract packet registrations and their IDs.
/// </summary>
public static class JavaProtocolParser
{
    /// <summary>
    /// Represents a packet registration from the Java protocol file.
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
        Status
    }

    /// <summary>
    /// Parses GameProtocols.java and extracts all packet registrations.
    /// </summary>
    public static List<PacketRegistration> ParseGameProtocols(string javaFilePath)
    {
        if (!File.Exists(javaFilePath))
        {
            throw new FileNotFoundException($"GameProtocols.java not found at: {javaFilePath}");
        }

        var content = File.ReadAllText(javaFilePath);
        var registrations = new List<PacketRegistration>();

        // Find the section boundaries - look for the assignments, not declarations
        var serverboundStart = content.IndexOf("SERVERBOUND_TEMPLATE =", StringComparison.Ordinal);
        var clientboundStart = content.IndexOf("CLIENTBOUND_TEMPLATE =", StringComparison.Ordinal);

        if (serverboundStart >= 0 && clientboundStart > serverboundStart)
        {
            var serverboundSection = content[serverboundStart..clientboundStart];
            registrations.AddRange(ParseSection(serverboundSection, PacketDirection.Serverbound));
        }

        if (clientboundStart >= 0)
        {
            var clientboundSection = content[clientboundStart..];
            registrations.AddRange(ParseSection(clientboundSection, PacketDirection.Clientbound));
        }

        return registrations;
    }

    private static List<PacketRegistration> ParseSection(string section, PacketDirection direction)
    {
        var registrations = new List<(int position, string className)>();

        // Normalize whitespace - collapse all whitespace to single spaces
        var normalizedSection = Regex.Replace(section, @"\s+", " ");

        // Pattern 1: Match .addPacket with STREAM_CODEC
        // .addPacket(GamePacketTypes.TYPE_NAME, PacketClassName.STREAM_CODEC)
        // .addPacket(GamePacketTypes.TYPE_NAME, PacketClassName.Pos.STREAM_CODEC)  <- nested class
        var streamCodecPattern = @"\.(addPacket|withBundlePacket)\s*\([^,]+,\s*(\w+)(Packet)?(?:\.(\w+))?\.(STREAM_CODEC|GAMEPLAY_STREAM_CODEC)";

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
            .Select((r, idx) => new PacketRegistration(r.className, idx, direction, ProtocolState.Play))
            .ToList();

        return sortedRegistrations;
    }

    private static string BuildClassName(string baseName, bool hasPacketSuffix, string? nestedClass)
    {
        // If there's a nested class suffix, we need to insert it before "Packet"
        // e.g., MoveEntityPacket + Pos => MoveEntityPosPacket
        if (!string.IsNullOrEmpty(nestedClass) && nestedClass != "STREAM_CODEC" && nestedClass != "GAMEPLAY_STREAM_CODEC")
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
