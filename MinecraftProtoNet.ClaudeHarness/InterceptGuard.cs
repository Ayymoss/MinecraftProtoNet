using System.Text;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// A latch that stops the bot reconnecting after something happened that a human needs to look at — an admin
/// teleporting it somewhere, freezing it, or otherwise taking an interest.
///
/// The rule is deliberately blunt: raise it, disconnect immediately, and refuse to start again until a person
/// acknowledges. A bot that quietly reconnects into a staff investigation turns a conversation into a ban, and
/// an unattended process cannot judge which it is looking at. The latch is a FILE, so it survives the process
/// dying, the machine rebooting, and any future scheduler that restarts runs automatically.
/// </summary>
public static class InterceptGuard
{
    /// <summary>Kept beside the checked-in reference data so it is somewhere a human actually looks.</summary>
    public static string HaltFilePath { get; } = Path.Combine(
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory,
        "_ServerReferences",
        "HALTED-admin-intercept.md");

    public static bool IsHalted() => File.Exists(HaltFilePath);

    public static string ReadNotice() => IsHalted() ? File.ReadAllText(HaltFilePath) : "";

    /// <summary>
    /// Records why the bot stopped. Never overwrites an existing notice: the FIRST intercept is the one that
    /// explains what happened, and a later trigger appending noise over it would lose that.
    /// </summary>
    public static void Raise(string reason, string details, string? position, IEnumerable<string> recentChat)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HaltFilePath)!);
            if (File.Exists(HaltFilePath))
            {
                File.AppendAllText(HaltFilePath,
                    $"\n\n---\n\nFurther trigger at {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {reason}\n{details}\n");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# BOT HALTED — possible admin intercept");
            sb.AppendLine();
            sb.AppendLine($"- When: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local)");
            sb.AppendLine($"- Trigger: **{reason}**");
            sb.AppendLine($"- Detail: {details}");
            if (position is not null) sb.AppendLine($"- Position at the time: {position}");
            sb.AppendLine();
            sb.AppendLine("The bot disconnected immediately and will REFUSE to connect again while this file exists.");
            sb.AppendLine();
            sb.AppendLine("Check the account in a normal client first. When you are satisfied, either delete this");
            sb.AppendLine("file or run the harness once with `--ack-intercept`, which deletes it for you.");
            sb.AppendLine();
            sb.AppendLine("## Chat leading up to it");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var line in recentChat) sb.AppendLine(line);
            sb.AppendLine("```");

            File.WriteAllText(HaltFilePath, sb.ToString());
        }
        catch (Exception ex)
        {
            // The latch failing to write must not also crash the disconnect path.
            Console.Error.WriteLine($"[intercept] could not write the halt file: {ex.Message}");
        }
    }

    public static void Acknowledge()
    {
        if (!IsHalted()) return;
        var archive = HaltFilePath.Replace(".md", $"-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        try
        {
            File.Move(HaltFilePath, archive);
            Console.WriteLine($"[intercept] acknowledged; previous notice kept at {archive}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[intercept] could not archive the notice: {ex.Message}");
        }
    }
}
