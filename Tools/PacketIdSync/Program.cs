using PacketIdSync;

public static class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        var dryRun = args.Contains("--dry-run") || args.Contains("-n");
        var positionalArgs = args.Where(a => !a.StartsWith('-')).ToArray();

        // Detect mode: auto-discover (directory) vs single-file (backward compat)
        var isSingleFileMode = positionalArgs.Length > 0 && positionalArgs[0].EndsWith(".java", StringComparison.OrdinalIgnoreCase);

        return isSingleFileMode ? RunSingleFileMode(positionalArgs, dryRun) : RunAutoDiscoverMode(positionalArgs, dryRun);
    }

    /// <summary>
    /// Original single-file mode for backward compatibility.
    /// Usage: PacketIdSync [protocol-file.java] [packets-dir] [--dry-run]
    /// </summary>
    private static int RunSingleFileMode(string[] positionalArgs, bool dryRun)
    {
        var protocolFile = positionalArgs.Length > 0 ? positionalArgs[0] : "";
        var packetsDir = positionalArgs.Length > 1 ? positionalArgs[1] : "";

        if (!Path.IsPathRooted(protocolFile))
            protocolFile = Path.Combine(Directory.GetCurrentDirectory(), protocolFile);
        if (!Path.IsPathRooted(packetsDir))
            packetsDir = Path.Combine(Directory.GetCurrentDirectory(), packetsDir);

        Console.WriteLine("=== Packet ID Sync Tool (Single-File Mode) ===");
        Console.WriteLine();
        Console.WriteLine($"Protocol file: {protocolFile}");
        Console.WriteLine($"Packets dir:   {packetsDir}");
        Console.WriteLine($"Dry run:       {dryRun}");
        Console.WriteLine();

        if (!File.Exists(protocolFile))
        {
            PrintError($"Protocol file not found: {protocolFile}");
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(packetsDir))
        {
            PrintError($"Packets directory not found: {packetsDir}");
            return 1;
        }

        var state = JavaProtocolParser.InferStateFromFileName(protocolFile) ?? JavaProtocolParser.ProtocolState.Play;

        try
        {
            Console.WriteLine($"Parsing {Path.GetFileName(protocolFile)} (state: {state})...");
            var registrations = JavaProtocolParser.ParseProtocolFile(protocolFile, state);

            var clientbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Clientbound).ToList();
            var serverbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Serverbound).ToList();

            Console.WriteLine($"Found {clientbound.Count} clientbound packets");
            Console.WriteLine($"Found {serverbound.Count} serverbound packets");
            Console.WriteLine();

            Console.WriteLine("Updating C# packet files...");
            var results = CSharpPacketUpdater.UpdatePacketDirectory(packetsDir, registrations, dryRun);
            CSharpPacketUpdater.PrintResults(results, dryRun);
            PrintMissingPackets(results, registrations);

            return 0;
        }
        catch (Exception ex)
        {
            PrintError(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Auto-discover mode: finds all *Protocols.java files and processes each state.
    /// Usage: PacketIdSync [java-references-dir] [packets-root-dir] [--dry-run]
    /// </summary>
    private static int RunAutoDiscoverMode(string[] positionalArgs, bool dryRun)
    {
        const string defaultJavaDir =
            @"C:\Users\Amos\RiderProjects\_Work\_Minecraft\MinecraftProtoNet\_JavaReferences\minecraft-26.1-REFERENCE-ONLY";
        const string defaultPacketsRoot = @"C:\Users\Amos\RiderProjects\_Work\_Minecraft\MinecraftProtoNet\MinecraftProtoNet.Core\Packets";

        var javaDir = positionalArgs.Length > 0 ? positionalArgs[0] : defaultJavaDir;
        var packetsRoot = positionalArgs.Length > 1 ? positionalArgs[1] : defaultPacketsRoot;

        if (!Path.IsPathRooted(javaDir))
            javaDir = Path.Combine(Directory.GetCurrentDirectory(), javaDir);
        if (!Path.IsPathRooted(packetsRoot))
            packetsRoot = Path.Combine(Directory.GetCurrentDirectory(), packetsRoot);

        Console.WriteLine("=== Packet ID Sync Tool (Auto-Discover Mode) ===");
        Console.WriteLine();
        Console.WriteLine($"Java references: {javaDir}");
        Console.WriteLine($"Packets root:    {packetsRoot}");
        Console.WriteLine($"Dry run:         {dryRun}");
        Console.WriteLine();

        if (!Directory.Exists(javaDir))
        {
            PrintError($"Java references directory not found: {javaDir}");
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(packetsRoot))
        {
            PrintError($"Packets root directory not found: {packetsRoot}");
            return 1;
        }

        // Discover all protocol files
        var protocolFiles = JavaProtocolParser.DiscoverProtocolFiles(javaDir);

        if (protocolFiles.Count == 0)
        {
            PrintError($"No *Protocols.java files found under: {javaDir}");
            return 1;
        }

        Console.WriteLine($"Discovered {protocolFiles.Count} protocol file(s):");
        foreach (var (filePath, state) in protocolFiles.OrderBy(p => p.State.ToString()))
        {
            Console.WriteLine($"  {state,-15} <- {Path.GetFileName(filePath)}");
        }

        Console.WriteLine();

        var allResults = new List<CSharpPacketUpdater.UpdateResult>();
        var allRegistrations = new List<JavaProtocolParser.PacketRegistration>();
        var hasErrors = false;

        foreach (var (filePath, state) in protocolFiles.OrderBy(p => p.State.ToString()))
        {
            var stateDirName = JavaProtocolParser.StateToCSharpDir[state];
            var packetsDir = Path.Combine(packetsRoot, stateDirName);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--- {state} ---");
            Console.ResetColor();

            if (!Directory.Exists(packetsDir))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  Skipping — C# directory not found: {packetsDir}");
                Console.ResetColor();
                Console.WriteLine();
                continue;
            }

            try
            {
                var registrations = JavaProtocolParser.ParseProtocolFile(filePath, state);
                allRegistrations.AddRange(registrations);

                var clientbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Clientbound).ToList();
                var serverbound = registrations.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Serverbound).ToList();

                Console.WriteLine($"  Parsed: {clientbound.Count} clientbound, {serverbound.Count} serverbound");

                var results = CSharpPacketUpdater.UpdatePacketDirectory(packetsDir, registrations, dryRun);
                allResults.AddRange(results);

                // Print per-state summary inline
                var updated = results.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated);
                var correct = results.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.AlreadyCorrect);
                var noMatch = results.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.NoMatch);

                if (updated > 0)
                {
                    Console.ForegroundColor = dryRun ? ConsoleColor.Yellow : ConsoleColor.Green;
                    Console.WriteLine($"  {(dryRun ? "Would update" : "Updated")}: {updated}");
                    foreach (var r in results.Where(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated).OrderBy(r => r.PacketName))
                    {
                        Console.WriteLine($"    {r.PacketName}: 0x{r.OldId:X2} -> 0x{r.NewId:X2}");
                    }

                    Console.ResetColor();
                }

                if (correct > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  Already correct: {correct}");
                    Console.ResetColor();
                }

                if (noMatch > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  UNMATCHED: {noMatch} (may be deleted/renamed)");
                    foreach (var r in results.Where(r => r.Status == CSharpPacketUpdater.UpdateStatus.NoMatch).OrderBy(r => r.PacketName))
                    {
                        Console.WriteLine($"    [!] {r.PacketName}");
                    }

                    Console.ResetColor();
                }

                // Print missing Java packets for this state
                PrintMissingPacketsForState(results, registrations, state);

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                hasErrors = true;
            }
        }

        // Print combined summary
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("--- Combined Summary ---");
        Console.ResetColor();

        var totalUpdated = allResults.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated);
        var totalCorrect = allResults.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.AlreadyCorrect);
        var totalNoMatch = allResults.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.NoMatch);
        var totalErrors = allResults.Count(r => r.Status == CSharpPacketUpdater.UpdateStatus.Error);

        var summary = $"Total: {totalUpdated} updated, {totalCorrect} correct";
        if (totalNoMatch > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            summary += $", {totalNoMatch} UNMATCHED";
        }

        if (totalErrors > 0)
        {
            summary += $", {totalErrors} errors";
        }

        Console.WriteLine(summary);
        Console.ResetColor();

        if (dryRun && totalUpdated > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("(Dry run — no files were modified)");
            Console.ResetColor();
        }

        return hasErrors ? 1 : 0;
    }

    /// <summary>
    /// Prints Java packets that have no C# implementation (single-file mode).
    /// </summary>
    private static void PrintMissingPackets(
        List<CSharpPacketUpdater.UpdateResult> results,
        List<JavaProtocolParser.PacketRegistration> registrations)
    {
        var matchedJavaPackets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results.Where(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated ||
                                                  r.Status == CSharpPacketUpdater.UpdateStatus.AlreadyCorrect))
        {
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
            Console.ResetColor();

            PrintUnmatchedList(unmatchedJavaPackets);
        }
    }

    /// <summary>
    /// Prints Java packets that have no C# implementation for a specific state (auto-discover mode).
    /// </summary>
    private static void PrintMissingPacketsForState(
        List<CSharpPacketUpdater.UpdateResult> results,
        List<JavaProtocolParser.PacketRegistration> registrations,
        JavaProtocolParser.ProtocolState state)
    {
        var matchedJavaPackets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results.Where(r => r.Status == CSharpPacketUpdater.UpdateStatus.Updated ||
                                                  r.Status == CSharpPacketUpdater.UpdateStatus.AlreadyCorrect))
        {
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
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"  Not implemented: {unmatchedJavaPackets.Count} Java packet(s)");
            Console.ResetColor();

            var cbMissing = unmatchedJavaPackets.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Clientbound).ToList();
            var sbMissing = unmatchedJavaPackets.Where(r => r.Direction == JavaProtocolParser.PacketDirection.Serverbound).ToList();

            if (cbMissing.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"    Clientbound ({cbMissing.Count}):");
                foreach (var p in cbMissing.OrderBy(p => p.PacketId))
                {
                    var csharpName = PacketNameMapper.JavaToCSharpName(p.JavaTypeName);
                    Console.WriteLine($"      0x{p.PacketId:X2}: {csharpName}");
                }

                Console.ResetColor();
            }

            if (sbMissing.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"    Serverbound ({sbMissing.Count}):");
                foreach (var p in sbMissing.OrderBy(p => p.PacketId))
                {
                    var csharpName = PacketNameMapper.JavaToCSharpName(p.JavaTypeName);
                    Console.WriteLine($"      0x{p.PacketId:X2}: {csharpName}");
                }

                Console.ResetColor();
            }
        }
    }

    private static void PrintUnmatchedList(List<JavaProtocolParser.PacketRegistration> unmatchedJavaPackets)
    {
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

            Console.ResetColor();
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

            Console.ResetColor();
        }
    }

    static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
        Console.ResetColor();
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Auto-discover:  PacketIdSync [java-references-dir] [packets-root-dir] [--dry-run]");
        Console.WriteLine("  Single file:    PacketIdSync <protocol-file.java> [packets-dir] [--dry-run]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  java-references-dir  Root directory containing *Protocols.java files");
        Console.WriteLine("  packets-root-dir     Root C# Packets/ directory (with Play/, Configuration/, etc.)");
        Console.WriteLine("  protocol-file.java   Single Java protocol file (backward compat mode)");
        Console.WriteLine("  packets-dir          Single C# packets subdirectory (backward compat mode)");
        Console.WriteLine("  --dry-run, -n        Preview changes without writing files");
    }
}
