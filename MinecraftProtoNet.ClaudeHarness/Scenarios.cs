namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// A scripted, self-contained test the harness can run end-to-end with no manual input.
/// </summary>
/// <param name="Name">Identifier used on the CLI (<c>--scenario parkour1</c>) and in artifact paths.</param>
/// <param name="Server">Test server host.</param>
/// <param name="Port">Test server port.</param>
/// <param name="Start">Block coord the bot is teleported to before the run (the parkour start).</param>
/// <param name="End">Goal block coord (the parkour finish).</param>
/// <param name="ReadyTimeoutSec">Max seconds to wait for connect + spawn + arrival at start.</param>
/// <param name="RunTimeoutSec">Max wall-clock seconds for the actual attempt before declaring Timeout.</param>
/// <param name="FallFloorY">Absolute Y below which the bot has fallen off the course (failed). Courses can
/// include legit drops >4 blocks, so an absolute floor is more reliable than a relative one.</param>
/// <param name="StuckTicks">Ticks without distance progress toward the goal before declaring Stuck.</param>
public sealed record Scenario(
    string Name,
    string Server,
    int Port,
    (int X, int Y, int Z) Start,
    (int X, int Y, int Z) End,
    int ReadyTimeoutSec,
    int RunTimeoutSec,
    double FallFloorY,
    int StuckTicks);

public static class Scenarios
{
    /// <summary>The ascending parkour: -925 71 -45 → -923 86 -3.</summary>
    public static readonly Scenario Parkour1 = new(
        Name: "parkour1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-925, 71, -45),
        End: (-923, 86, -3),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 45,
        FallFloorY: 66,
        StuckTicks: 200);

    public static Scenario? Get(string name) => name switch
    {
        "parkour1" => Parkour1,
        _ => null
    };
}
