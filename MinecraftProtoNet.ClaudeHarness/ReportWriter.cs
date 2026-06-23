using System.Text;
using System.Text.Json;
using MinecraftProtoNet.Baritone.Api;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>Emits the two artifacts Claude reads after a run: summary.json (machine) + report.md (human).</summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static void Write(string runDir, Scenario s, RunOutcome outcome, TelemetryRecorder rec, IBaritone baritone, long durationTicks)
    {
        var term = rec.TerminalSample;
        var path = TryDumpPath(baritone);

        var summary = new
        {
            scenario = s.Name,
            outcome = outcome.ToString(),
            detail = rec.TerminalDetail,
            durationTicks,
            start = new { s.Start.X, s.Start.Y, s.Start.Z },
            end = new { s.End.X, s.End.Y, s.End.Z },
            highestGroundY = rec.HighestGroundY > double.MinValue ? rec.HighestGroundY : (double?)null,
            terminalSample = term,
            failingMovement = term is null ? null : new
            {
                type = term.MovementType,
                index = term.PathIndex,
                length = term.PathLength,
                dest = new { x = term.MovementDestX, y = term.MovementDestY, z = term.MovementDestZ }
            },
            intendedPath = rec.IntendedPath,
            computedPath = path
        };
        File.WriteAllText(Path.Combine(runDir, "summary.json"), JsonSerializer.Serialize(summary, Pretty));
        File.WriteAllText(Path.Combine(runDir, "report.md"), BuildReport(s, outcome, rec, path, durationTicks));
    }

    private static string BuildReport(Scenario s, RunOutcome outcome, TelemetryRecorder rec, List<PathStep>? path, long durationTicks)
    {
        var term = rec.TerminalSample;
        var sb = new StringBuilder();
        sb.AppendLine($"# Parkour run — {outcome}");
        sb.AppendLine();
        sb.AppendLine($"- **Scenario**: {s.Name}");
        sb.AppendLine($"- **Outcome**: {outcome}");
        sb.AppendLine($"- **Detail**: {rec.TerminalDetail}");
        sb.AppendLine($"- **Duration**: {durationTicks} ticks");
        sb.AppendLine($"- **Start**: ({s.Start.X}, {s.Start.Y}, {s.Start.Z})  **End**: ({s.End.X}, {s.End.Y}, {s.End.Z})");
        sb.AppendLine($"- **Highest ground Y reached**: {(rec.HighestGroundY > double.MinValue ? rec.HighestGroundY.ToString("F2") : "n/a")}");
        sb.AppendLine();

        sb.AppendLine("## Failing movement (at terminal tick)");
        if (term is not null)
        {
            sb.AppendLine($"- type: `{term.MovementType ?? "n/a"}`  index: {term.PathIndex}/{term.PathLength}");
            sb.AppendLine($"- dest: ({term.MovementDestX},{term.MovementDestY},{term.MovementDestZ})");
            sb.AppendLine($"- pos at terminal: ({term.X},{term.Y},{term.Z}) onGround={term.OnGround} vel=({term.VelX},{term.VelY},{term.VelZ})");
        }
        else sb.AppendLine("- n/a (no telemetry captured)");
        sb.AppendLine();

        sb.AppendLine("## Last ticks (oldest → newest)");
        sb.AppendLine("| tick | ph | x | y | z | grnd | velY | inputs | movement | idx | dist |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var t in rec.Recent())
        {
            sb.AppendLine($"| {t.Tick} | {t.Phase} | {t.X:F2} | {t.Y:F2} | {t.Z:F2} | {(t.OnGround ? "Y" : "-")} | {t.VelY:F2} | {Inputs(t)} | {t.MovementType ?? "-"} | {t.PathIndex} | {t.DistToGoal:F1} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Intended path (Baritone's first plan — what it meant to do)");
        if (rec.IntendedPath is { Count: > 0 })
            foreach (var step in rec.IntendedPath)
                sb.AppendLine($"- {step}");
        else
            sb.AppendLine("- n/a (no path was computed)");
        sb.AppendLine();

        sb.AppendLine("## Computed path (current at teardown)");
        if (path is { Count: > 0 })
            foreach (var step in path)
                sb.AppendLine($"- [{step.Index}] `{step.Type}` ({step.SrcX},{step.SrcY},{step.SrcZ}) → ({step.DestX},{step.DestY},{step.DestZ})");
        else
            sb.AppendLine("- n/a (no current path at teardown)");

        return sb.ToString();
    }

    private static string Inputs(TelemetrySample t)
    {
        var parts = new List<string>(4);
        if (t.Forward) parts.Add("F");
        if (t.Backward) parts.Add("B");
        if (t.Left) parts.Add("L");
        if (t.Right) parts.Add("R");
        if (t.Jump) parts.Add("Jmp");
        if (t.Sprint) parts.Add("Spr");
        if (t.Shift) parts.Add("Snk");
        if (t.ClickLeft) parts.Add("cL");
        if (t.ClickRight) parts.Add("cR");
        return parts.Count == 0 ? "-" : string.Join(" ", parts);
    }

    private sealed record PathStep(int Index, string Type, int SrcX, int SrcY, int SrcZ, int DestX, int DestY, int DestZ);

    private static List<PathStep>? TryDumpPath(IBaritone baritone)
    {
        try
        {
            var exec = baritone.GetPathingBehavior().GetCurrent();
            if (exec is null) return null;
            var moves = exec.GetPath().Movements();
            var list = new List<PathStep>(moves.Count);
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                var src = m.GetSrc();
                var d = m.GetDest();
                list.Add(new PathStep(i, m.GetType().Name, src.X, src.Y, src.Z, d.X, d.Y, d.Z));
            }
            return list;
        }
        catch
        {
            return null;
        }
    }
}
