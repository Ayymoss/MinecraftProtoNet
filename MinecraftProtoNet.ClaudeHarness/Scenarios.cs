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
/// <param name="ClearBoxes">Regions cleared by <c>--clear-course</c>, as inclusive block corners. Only
/// bot-placed dirt is removed (<c>/fill ... air replace dirt</c>), so bedrock courses and lava are untouched.
/// Each box must stay under the 32768-block /fill limit, hence a list rather than one big box.
/// Ignored when <paramref name="BuildCommands"/> is set.</param>
/// <param name="Inventory">Commands run to stock the bot after <c>clear @s</c>. Courses that require mining
/// need a tool here, not just throwaway blocks.</param>
/// <param name="BuildCommands">If set, <c>--clear-course</c> RESTORES THE COURSE by re-running the full build
/// instead of doing a dirt-only fill. This is the correct reset for courses containing breakable blocks: the
/// build begins with an air fill, so bot-placed dirt is wiped AND any stone the bot mined is put back, giving
/// a pristine course every run.</param>
public sealed record Scenario(
    string Name,
    string Server,
    int Port,
    (int X, int Y, int Z) Start,
    (int X, int Y, int Z) End,
    int ReadyTimeoutSec,
    int RunTimeoutSec,
    double FallFloorY,
    int StuckTicks,
    IReadOnlyList<(int X1, int Y1, int Z1, int X2, int Y2, int Z2)> ClearBoxes,
    IReadOnlyList<string>? Inventory = null,
    IReadOnlyList<string>? BuildCommands = null,
    int GoalYTolerance = 1,
    bool RequireOnGround = false,
    bool GoalDrivenTermination = true);

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
        StuckTicks: 200,
        // 19 x 27 x 47 = 24,111 blocks (< 32768 /fill limit).
        ClearBoxes: [(-938, 64, -47, -920, 90, -1)]);

    /// <summary>
    /// Vertical ascent in a single column: -916 71 -45 → -916 81 -45 (10 blocks straight up), inventory is
    /// dirt only. Exercises the pillar/place path rather than parkour: the bot has to build its own route up.
    /// The start platform is 3x3 and the top platform is 7x7, so the top overhangs the bottom — the bot has to
    /// pillar outside the overhang footprint and come back onto its edge rather than going straight up.
    /// NOTE: --clear-course only fills x -938..-920, so it does NOT cover this column.
    /// </summary>
    public static readonly Scenario Pillar1 = new(
        Name: "pillar1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-916, 71, -45),
        End: (-916, 81, -45),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 60,
        FallFloorY: 66,
        StuckTicks: 200,
        // Start/top platforms are bedrock (verified via --diag), so a dirt-only fill cannot damage them.
        // Covers the column plus the detour the bot takes around the 7x7 overhang (out to z -52).
        // 13 x 27 x 19 = 6,669 blocks (< 32768 /fill limit).
        ClearBoxes: [(-922, 64, -52, -910, 90, -34)]);

    /// <summary>
    /// Mixed-discipline course built by <see cref="Courses.Course1"/>: steps, parkour gaps, a ladder climb, a
    /// drop into water, and three stone walls that must be mined through. Bot gets a pickaxe plus dirt.
    /// -863 73 -38 → -883 87 -27.
    /// </summary>
    public static readonly Scenario Course1 = new(
        Name: "course1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-863, 73, -38),
        End: (-892, 87, -27),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 120,
        FallFloorY: 70,
        StuckTicks: 300,
        ClearBoxes: [],
        // Pickaxe + dirt. Removing dirt did force the swim in principle, but it also removed every recovery
        // option: with the ladder-top gap unsolvable the goal went unreachable and the bot bailed off the
        // course. Blocks back in for now so the gap and the swim can be judged separately - with dirt it can
        // bridge the gap, at the cost of being able to bridge the canal wall too.
        Inventory: ["give @s minecraft:diamond_pickaxe 1"],
        // Reset = rebuild, so mined stone is restored and stray dirt removed.
        BuildCommands: Courses.Course1);

    /// <summary>
    /// Minimal ladder-top test: climb an 11-rung ladder and STAND ON TOP of it. The goal block is the
    /// ladder's top face, so the path must include the final climb - unlike course1, where the ladder top was
    /// a dead end and the bot had no reason to go there. Sky-built on a bedrock floor: no surface water to
    /// dive into, and a slip is a short drop. Pickaxe only (bedrock is unbreakable) so it cannot pillar up
    /// beside the ladder instead of climbing it.
    /// </summary>
    public static readonly Scenario Ladder1 = new(
        Name: "ladder1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-863, 129, -63),
        End: (-863, 140, -60),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 60,
        FallFloorY: 126,
        StuckTicks: 200,
        ClearBoxes: [],
        Inventory: ["give @s minecraft:diamond_pickaxe 1"],
        BuildCommands: Courses.Ladder1,
        // Exact: y139 is still clinging to the top rung, only y140 is standing on the ladder's top face.
        GoalYTolerance: 0,
        RequireOnGround: true);

    /// <summary>Control for <see cref="Ladder1"/>: same tower with a platform flush with the ladder top.</summary>
    public static readonly Scenario Ladder2 = Ladder1 with
    {
        Name = "ladder2",
        End = (-863, 140, -62),
        BuildCommands = Courses.Ladder2
    };

    /// <summary>Mine a tunnel through a wall three blocks thick.</summary>
    public static readonly Scenario Tunnel1 = new(
        Name: "tunnel1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-866, 129, -90),
        End: (-858, 129, -90),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 90,
        FallFloorY: 126,
        StuckTicks: 300,
        ClearBoxes: [],
        Inventory: ["give @s minecraft:diamond_pickaxe 1"],
        BuildCommands: Courses.Tunnel1);

    /// <summary>Open two closed fence gates set in full-width walls (no way around).</summary>
    public static readonly Scenario Gate1 = new(
        Name: "gate1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-866, 129, -110),
        End: (-856, 129, -110),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 60,
        FallFloorY: 126,
        StuckTicks: 200,
        ClearBoxes: [],
        Inventory: ["give @s minecraft:diamond_pickaxe 1"],
        BuildCommands: Courses.Gate1);

    /// <summary>
    /// Villager trading task (not a movement course): walk to the nearest villager, open its trade menu, read
    /// the offers, spawn exactly what the chosen offer costs, execute it and end up holding an emerald.
    /// Inventory starts empty on purpose - the cost is discovered from the villager, not assumed.
    /// </summary>
    public static readonly Scenario Villager1 = new(
        Name: "villager1",
        Server: "10.10.1.20",
        Port: 25565,
        Start: (-910, 64, -34),
        End: (-910, 64, -34),
        ReadyTimeoutSec: 45,
        RunTimeoutSec: 120,
        FallFloorY: 55,
        StuckTicks: 100000,
        ClearBoxes: [],
        Inventory: ["clear @s"],
        // The verdict comes from the TASK (did we end up holding an emerald?), never from position. Start and
        // End are the same block here, so position-based termination would report Success the instant the run
        // began - a test that cannot fail.
        GoalDrivenTermination: false);

    public static Scenario? Get(string name) => name switch
    {
        "parkour1" => Parkour1,
        "pillar1" => Pillar1,
        "course1" => Course1,
        "ladder1" => Ladder1,
        "ladder2" => Ladder2,
        "tunnel1" => Tunnel1,
        "gate1" => Gate1,
        "villager1" => Villager1,
        _ => null
    };
}
