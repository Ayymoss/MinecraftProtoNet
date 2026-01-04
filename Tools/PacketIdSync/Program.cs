using PacketIdSync;

// Default paths (relative to solution root)
const string defaultProtocolFile = @"C:\Users\Amos\RiderProjects\_Work\MinecraftProtoNet.Core\Tools\PacketIdSync\bin\Debug\net10.0\Protocol\GameProtocols.java";
const string defaultPacketsDir = @"C:\Users\Amos\RiderProjects\_Work\MinecraftProtoNet.Core\MinecraftProtoNet.Core.Core\Packets\Play";

// Parse command line arguments
var protocolFile = args.Length > 0 ? args[0] : defaultProtocolFile;
var packetsDir = args.Length > 1 ? args[1] : defaultPacketsDir;
var dryRun = args.Contains("--dry-run") || args.Contains("-n");

// Resolve paths relative to current directory if not absolute
if (!Path.IsPathRooted(protocolFile))
{
    protocolFile = Path.Combine(Directory.GetCurrentDirectory(), protocolFile);
}
if (!Path.IsPathRooted(packetsDir))
{
    packetsDir = Path.Combine(Directory.GetCurrentDirectory(), packetsDir);
}

Console.WriteLine("=== Packet ID Sync Tool ===");
Console.WriteLine();
Console.WriteLine($"Protocol file: {protocolFile}");
Console.WriteLine($"Packets dir:   {packetsDir}");
Console.WriteLine($"Dry run:       {dryRun}");
Console.WriteLine();

// Validate inputs
if (!File.Exists(protocolFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Protocol file not found: {protocolFile}");
    Console.WriteLine();
    Console.WriteLine("Usage: PacketIdSync [protocol-file] [packets-dir] [--dry-run]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  protocol-file  Path to GameProtocols.java (default: Protocol/GameProtocols.java)");
    Console.WriteLine("  packets-dir    Path to C# packets directory (default: MinecraftProtoNet.Core/Packets/Play)");
    Console.WriteLine("  --dry-run, -n  Preview changes without writing files");
    Console.ResetColor();
    return 1;
}

if (!Directory.Exists(packetsDir))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Packets directory not found: {packetsDir}");
    Console.ResetColor();
    return 1;
}

try
{
    // Parse Java protocol file
    Console.WriteLine("Parsing GameProtocols.java...");
    var registrations = JavaProtocolParser.ParseGameProtocols(protocolFile);
    
    var clientbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Clientbound).ToList();
    var serverbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Serverbound).ToList();
    
    Console.WriteLine($"Found {clientbound.Count} clientbound packets");
    Console.WriteLine($"Found {serverbound.Count} serverbound packets");
    Console.WriteLine();

    // Update C# packet files
    Console.WriteLine("Updating C# packet files...");
    var results = CSharpPacketUpdater.UpdatePacketDirectory(packetsDir, registrations, dryRun);

    // Print results
    CSharpPacketUpdater.PrintResults(results, dryRun);
    
    // Check for NEW Java packets that don't have C# implementations
    var matchedJavaPackets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var result in results.Where(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated || 
                                               r.Status == CSharpPacketUpdater.UpdateStatus.AlreadyCorrect))
    {
        // Find the Java packet that was matched
        var csharpName = result.PacketName;
        var reg = PacketNameMapper.FindMatchingRegistration(
            csharpName, 
            result.FilePath.Contains("Clientbound") ? "Clientbound" : "Serverbound", 
            registrations);
        if (reg != null)
        {
            matchedJavaPackets.Add(reg.JavaTypeName);
        }
    }
    
    var unmatchedJavaPackets = registrations
        .Where(r => !matchedJavaPackets.Contains(r.JavaTypeName))
        .ToList();
    
    if (unmatchedJavaPackets.Any())
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"=== {unmatchedJavaPackets.Count} Java packets have NO C# implementation ===");
        Console.WriteLine("These are packets from Minecraft that you haven't implemented yet:");
        Console.ResetColor();
        
        var cbMissing = unmatchedJavaPackets.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Clientbound).ToList();
        var sbMissing = unmatchedJavaPackets.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Serverbound).ToList();
        
        if (cbMissing.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\nClientbound ({cbMissing.Count}):");
            foreach (var p in cbMissing.OrderBy(p => p.PacketId))
            {
                var csharpName = PacketNameMapper.JavaToCSharpName(p.JavaTypeName);
                Console.WriteLine($"  0x{p.PacketId:X2}: {csharpName}");
            }
        }
        
        if (sbMissing.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\nServerbound ({sbMissing.Count}):");
            foreach (var p in sbMissing.OrderBy(p => p.PacketId))
            {
                var csharpName = PacketNameMapper.JavaToCSharpName(p.JavaTypeName);
                Console.WriteLine($"  0x{p.PacketId:X2}: {csharpName}");
            }
        }
        Console.ResetColor();
    }

    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 1;
}
