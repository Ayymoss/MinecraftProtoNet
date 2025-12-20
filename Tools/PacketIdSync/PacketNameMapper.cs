namespace PacketIdSync;

/// <summary>
/// Maps between Java packet names and C# packet names.
/// </summary>
public static class PacketNameMapper
{
    /// <summary>
    /// Converts a Java packet class name to the expected C# packet class name.
    /// </summary>
    /// <param name="javaName">The Java class name (e.g., "ClientboundLoginPacket")</param>
    /// <returns>The C# class name (e.g., "LoginPacket")</returns>
    public static string JavaToCSharpName(string javaName)
    {
        // Remove "Clientbound" or "Serverbound" prefix
        var csharpName = javaName;
        
        if (csharpName.StartsWith("Clientbound", StringComparison.OrdinalIgnoreCase))
        {
            csharpName = csharpName["Clientbound".Length..];
        }
        else if (csharpName.StartsWith("Serverbound", StringComparison.OrdinalIgnoreCase))
        {
            csharpName = csharpName["Serverbound".Length..];
        }

        // Handle special name mappings (Java uses different names than C#)
        csharpName = ApplySpecialMappings(csharpName);

        return csharpName;
    }

    /// <summary>
    /// Converts a C# packet class name to find matching Java names.
    /// </summary>
    public static (string clientboundName, string serverboundName) CSharpToJavaNames(string csharpName)
    {
        // Handle reverse special mappings
        var javaBaseName = ReverseSpecialMappings(csharpName);
        
        return ($"Clientbound{javaBaseName}", $"Serverbound{javaBaseName}");
    }

    private static string ApplySpecialMappings(string name) => name switch
    {
        // Java uses nested classes like MoveEntityPacket.Pos -> MoveEntityPosPacket
        // Map to C# naming convention
        "MovePlayerPosPacket" => "MovePlayerPositionPacket",
        "MovePlayerPosRotPacket" => "MovePlayerPositionRotationPacket",
        "MovePlayerRotPacket" => "MovePlayerRotationPacket",
        "MovePlayerStatusOnlyPacket" => "MovePlayerPositionPacket",  // Maps to position-only
        "MoveEntityPosPacket" => "MoveEntityPositionPacket",
        "MoveEntityPosRotPacket" => "MoveEntityPositionRotationPacket",
        "MoveEntityRotPacket" => "MoveEntityRotationPacket",
        
        // Java abbreviations
        "BlockChangedAckPacket" => "BlockChangedAcknowledgementPacket",
        
        // Add more mappings as needed
        _ => name
    };

    private static string ReverseSpecialMappings(string name) => name switch
    {
        // Reverse mappings for C# -> Java lookup
        "MovePlayerPositionPacket" => "MovePlayerPosPacket",
        "MovePlayerPositionRotationPacket" => "MovePlayerPosRotPacket",
        "MovePlayerRotationPacket" => "MovePlayerRotPacket",
        "MoveEntityPositionPacket" => "MoveEntityPosPacket",
        "MoveEntityPositionRotationPacket" => "MoveEntityPosRotPacket",
        "MoveEntityRotationPacket" => "MoveEntityRotPacket",
        "BlockChangedAcknowledgementPacket" => "BlockChangedAckPacket",
        "BundleDelimiterPacket" => "BundlePacket",
        
        _ => name
    };

    /// <summary>
    /// Attempts to match a C# packet file to a Java packet registration.
    /// </summary>
    public static JavaProtocolParser.PacketRegistration? FindMatchingRegistration(
        string csharpClassName,
        string packetDirectory,  // e.g., "Clientbound" or "Serverbound"
        IEnumerable<JavaProtocolParser.PacketRegistration> registrations)
    {
        var direction = packetDirectory.Contains("Clientbound", StringComparison.OrdinalIgnoreCase)
            ? JavaProtocolParser.PacketDirection.Clientbound
            : JavaProtocolParser.PacketDirection.Serverbound;

        // Get the expected Java name variants
        var (clientboundName, serverboundName) = CSharpToJavaNames(csharpClassName);
        var expectedJavaName = direction == JavaProtocolParser.PacketDirection.Clientbound 
            ? clientboundName 
            : serverboundName;

        // Try exact match first
        var match = registrations.FirstOrDefault(r => 
            r.Direction == direction && 
            r.JavaTypeName.Equals(expectedJavaName, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // Try fuzzy match - find any registration containing the C# name
        var nameWithoutPacket = csharpClassName.Replace("Packet", "");
        match = registrations.FirstOrDefault(r =>
            r.Direction == direction &&
            r.JavaTypeName.Contains(nameWithoutPacket, StringComparison.OrdinalIgnoreCase));

        return match;
    }
}
