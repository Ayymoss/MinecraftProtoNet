namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Lightweight in-memory diagnostic buffer for movement execution tracing. Append-only (no per-line I/O), so it
/// does NOT perturb game-loop timing the way Console.WriteLine does. The harness flushes <see cref="Lines"/> to a
/// file after the run. Gated by <see cref="Enabled"/> (set false in production).
/// </summary>
public static class MovementDiag
{
    public static bool Enabled;
    public static readonly List<string> Lines = new();

    public static void Log(string line)
    {
        if (Enabled) Lines.Add(line);
    }
}
