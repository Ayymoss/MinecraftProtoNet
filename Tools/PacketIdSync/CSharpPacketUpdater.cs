using System.Text.RegularExpressions;

namespace PacketIdSync;

/// <summary>
/// Updates C# packet files with correct packet IDs.
/// </summary>
public static class CSharpPacketUpdater
{
    /// <summary>
    /// Result of a packet update operation.
    /// </summary>
    public record UpdateResult(
        string FilePath,
        string PacketName,
        int? OldId,
        int? NewId,
        UpdateStatus Status,
        string? Message = null
    );

    public enum UpdateStatus
    {
        Updated,
        AlreadyCorrect,
        NotFound,
        NoMatch,
        Error
    }

    /// <summary>
    /// Scans and updates all packet files in a directory.
    /// </summary>
    public static List<UpdateResult> UpdatePacketDirectory(
        string packetsDirectory,
        IReadOnlyList<JavaProtocolParser.PacketRegistration> registrations,
        bool dryRun = false)
    {
        var results = new List<UpdateResult>();

        // Scan Clientbound and Serverbound subdirectories
        var clientboundDir = Path.Combine(packetsDirectory, "Clientbound");
        var serverboundDir = Path.Combine(packetsDirectory, "Serverbound");

        if (Directory.Exists(clientboundDir))
        {
            results.AddRange(UpdateDirectory(clientboundDir, "Clientbound", registrations, dryRun));
        }

        if (Directory.Exists(serverboundDir))
        {
            results.AddRange(UpdateDirectory(serverboundDir, "Serverbound", registrations, dryRun));
        }

        return results;
    }

    private static List<UpdateResult> UpdateDirectory(
        string directory,
        string directionName,
        IReadOnlyList<JavaProtocolParser.PacketRegistration> registrations,
        bool dryRun)
    {
        var results = new List<UpdateResult>();
        var csFiles = Directory.GetFiles(directory, "*.cs");

        foreach (var file in csFiles)
        {
            var result = UpdatePacketFile(file, directionName, registrations, dryRun);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static UpdateResult? UpdatePacketFile(
        string filePath,
        string directionName,
        IReadOnlyList<JavaProtocolParser.PacketRegistration> registrations,
        bool dryRun)
    {
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Find the [Packet(...)] attribute
        var packetAttrPattern = @"\[Packet\s*\(\s*(0x[0-9A-Fa-f]+)\s*,\s*ProtocolState\.(\w+)(?:\s*,\s*(true|false))?\s*\)\]";
        var match = Regex.Match(content, packetAttrPattern);

        if (!match.Success)
        {
            // No packet attribute found - might not be a packet file
            return null;
        }

        var currentIdHex = match.Groups[1].Value;
        var currentId = Convert.ToInt32(currentIdHex, 16);
        var state = match.Groups[2].Value;

        // Find matching Java registration
        var registration = PacketNameMapper.FindMatchingRegistration(fileName, directionName, registrations);

        if (registration == null)
        {
            return new UpdateResult(filePath, fileName, currentId, null, UpdateStatus.NoMatch,
                $"No matching Java packet found for {fileName}");
        }

        var newId = registration.PacketId;

        if (currentId == newId)
        {
            return new UpdateResult(filePath, fileName, currentId, newId, UpdateStatus.AlreadyCorrect);
        }

        // Update the file
        var newIdHex = $"0x{newId:X2}";
        var newContent = Regex.Replace(content, packetAttrPattern, m =>
        {
            var thirdArg = m.Groups[3].Success ? $", {m.Groups[3].Value}" : "";
            return $"[Packet({newIdHex}, ProtocolState.{state}{thirdArg})]";
        });

        if (!dryRun)
        {
            File.WriteAllText(filePath, newContent);
        }

        return new UpdateResult(filePath, fileName, currentId, newId, UpdateStatus.Updated);
    }

    /// <summary>
    /// Prints a summary of update results to the console.
    /// </summary>
    public static void PrintResults(List<UpdateResult> results, bool dryRun)
    {
        var updated = results.Where(r => r.Status == UpdateStatus.Updated).ToList();
        var correct = results.Where(r => r.Status == UpdateStatus.AlreadyCorrect).ToList();
        var noMatch = results.Where(r => r.Status == UpdateStatus.NoMatch).ToList();
        var errors = results.Where(r => r.Status == UpdateStatus.Error).ToList();

        Console.WriteLine();
        Console.WriteLine("=== Packet ID Sync Results ===");
        Console.WriteLine();

        if (updated.Any())
        {
            Console.ForegroundColor = dryRun ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine($"{(dryRun ? "[DRY RUN] Would update" : "Updated")} {updated.Count} packet(s):");
            Console.ResetColor();
            
            foreach (var r in updated.OrderBy(r => r.PacketName))
            {
                Console.WriteLine($"  {r.PacketName}: 0x{r.OldId:X2} -> 0x{r.NewId:X2}");
            }
            Console.WriteLine();
        }

        if (correct.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Already correct: {correct.Count} packet(s)");
            Console.ResetColor();
        }

        // Make unmatched packets VERY prominent - these may be deleted/renamed
        if (noMatch.Any())
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(new string('!', 60));
            Console.WriteLine($"WARNING: {noMatch.Count} C# packet(s) have NO MATCH in Java reference!");
            Console.WriteLine("These packets may have been DELETED or RENAMED by Minecraft.");
            Console.WriteLine("Review these and consider removing or renaming them:");
            Console.WriteLine(new string('!', 60));
            Console.ResetColor();
            
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            foreach (var r in noMatch.OrderBy(r => r.PacketName))
            {
                Console.WriteLine($"  [!] {r.PacketName}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        if (errors.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Errors: {errors.Count}");
            Console.ResetColor();
            
            foreach (var r in errors)
            {
                Console.WriteLine($"  {r.PacketName}: {r.Message}");
            }
        }

        Console.WriteLine();
        var statusLine = $"Summary: {updated.Count} updated, {correct.Count} correct";
        if (noMatch.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            statusLine += $", {noMatch.Count} UNMATCHED (review!)";
        }
        else
        {
            statusLine += ", 0 unmatched";
        }
        if (errors.Any())
        {
            statusLine += $", {errors.Count} errors";
        }
        Console.WriteLine(statusLine);
        Console.ResetColor();
    }
}
