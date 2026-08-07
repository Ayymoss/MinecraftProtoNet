using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Utilities;
using MinecraftProtoNet.ClaudeHarness;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.Utilities;

string? GetArg(string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name)
            return args[i + 1];
    return null;
}

// Locate Webcore's Configuration dir (active-account.json + MSAL cache) so the harness reuses its login.
// Harness bin is <sln>/MinecraftProtoNet.ClaudeHarness/bin/<Cfg>/<tfm>; mirror <Cfg>/<tfm> for Bot.Webcore.
static string? GuessWebcoreConfigDir()
{
    try
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var tfm = dir.Name;
        var cfg = dir.Parent?.Name;
        var slnRoot = dir.Parent?.Parent?.Parent?.Parent?.FullName;
        if (cfg is null || slnRoot is null) return null;
        var candidate = Path.Combine(slnRoot, "Bot.Webcore", "bin", cfg, tfm, "Configuration");
        return Directory.Exists(candidate) ? candidate : null;
    }
    catch
    {
        return null;
    }
}

// --recon runs a read-only visit to a public server instead of a scenario: no world edits, no pathing, no
// teleports (none of which we could do there anyway). It shares only auth + connection with the scenario path.
var reconName = GetArg("--recon");
ReconProfile? reconProfile = null;
if (reconName is not null)
{
    if (!ReconProfile.All.TryGetValue(reconName, out reconProfile))
    {
        Console.Error.WriteLine($"[harness] Unknown recon profile '{reconName}'. Known: {string.Join(", ", ReconProfile.All.Keys)}");
        return 2;
    }
}

var scenarioName = GetArg("--scenario") ?? "parkour1";
var scenario = Scenarios.Get(scenarioName);
if (scenario is null)
{
    Console.Error.WriteLine($"[harness] Unknown scenario '{scenarioName}'. Known: parkour1");
    return 2;
}

if (GetArg("--server") is { } serverOverride) scenario = scenario with { Server = serverOverride };
if (GetArg("--port") is { } portStr && int.TryParse(portStr, out var port)) scenario = scenario with { Port = port };

// Share Webcore's saved login via MCPROTO_CONFIG_DIR. Must be set before any auth type initializes.
var configDir = GetArg("--config-dir")
    ?? Environment.GetEnvironmentVariable("MCPROTO_CONFIG_DIR")
    ?? GuessWebcoreConfigDir();
if (configDir is not null)
{
    Environment.SetEnvironmentVariable("MCPROTO_CONFIG_DIR", configDir);
    Console.WriteLine($"[harness] auth config dir: {configDir}");
}
else
{
    Console.WriteLine("[harness] WARNING: could not locate Webcore Configuration dir; auth will use the harness-local dir and likely fail. Pass --config-dir <path>.");
}

// Pin content root to the binary's directory so the copied appsettings.json loads regardless of cwd.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddMinecraftClient(builder.Configuration);
builder.Services.AddBaritone();
using var host = builder.Build();

// Initialize the item registry and wire the static registry into EntityInventory + Baritone
// (same startup steps as Bot.Webcore/Program.cs). Required before any pathing/inventory logic runs.
var registryService = host.Services.GetRequiredService<IItemRegistryService>();
await registryService.InitializeAsync();
EntityInventory.SetRegistryService(registryService);
Baritone.SetItemRegistryService(registryService);

// Force-construct the Baritone↔GameLoop hook (same as Bot.Webcore/Program.cs).
// Fully qualified: ServiceCollectionExtensions exists in both Core.Utilities and Baritone.Utilities.
host.Services.GetRequiredService<MinecraftProtoNet.Baritone.Utilities.ServiceCollectionExtensions.BaritoneGameLoopHook>();

// Disable anti-cheat aim humanization for clean validation — prove the core movement works without evasion
// noise. The Humanizer (tick/rotation jitter, chat throttle, etc.) is already off in the harness appsettings;
// these two are Baritone's aim-jitter settings, which add ±~1° per-tick yaw noise (RandomLooking113=2.0) that
// makes the near-vertical pillar place-raycast flicker. Set to 0 for deterministic aiming during testing.
MinecraftProtoNet.Baritone.Api.BaritoneAPI.GetSettings().RandomLooking.Value = 0;
MinecraftProtoNet.Baritone.Api.BaritoneAPI.GetSettings().RandomLooking113.Value = 0;
Console.WriteLine("[harness] anti-cheat aim jitter disabled (RandomLooking + RandomLooking113 = 0)");

// Non-perturbing in-memory movement trace (flushed to the run dir after the run).
MinecraftProtoNet.Baritone.Utils.MovementDiag.Enabled = true;

var client = host.Services.GetRequiredService<IMinecraftClient>();
var gameLoop = host.Services.GetRequiredService<IGameLoop>();
var baritoneProvider = host.Services.GetRequiredService<IBaritoneProvider>();

// --list-accounts / --account <match>: pick which cached Microsoft account the run authenticates as.
// Needed to A/B the same route on accounts with different server-side stats (gear speed, jump boost, etc.),
// which is otherwise impossible to hold constant.
// NOTE: the selection is PERSISTED to active-account.json — it is the same setting the web UI drives, so it
// outlives this run. The previous value is logged so it can be put back.
{
    var accountManager = host.Services.GetRequiredService<MinecraftProtoNet.Core.Auth.Managers.IAccountManager>();

    if (Array.IndexOf(args, "--list-accounts") >= 0)
    {
        foreach (var a in await accountManager.ListAccountsAsync())
        {
            Console.WriteLine($"[harness] account {(a.IsActive ? "*" : " ")} {a.Username}  ({a.HomeAccountId})");
        }
        return 0;
    }

    if (GetArg("--account") is { } wanted)
    {
        var accounts = await accountManager.ListAccountsAsync();
        var match = accounts.FirstOrDefault(a =>
            a.Username.Contains(wanted, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            Console.Error.WriteLine($"[harness] no cached account matches '{wanted}'. Known:");
            foreach (var a in accounts) Console.Error.WriteLine($"[harness]   {a.Username}");
            return 1;
        }

        var previous = accounts.FirstOrDefault(a => a.IsActive);
        await accountManager.SetActiveAccountAsync(match.HomeAccountId);
        Console.WriteLine($"[harness] active account -> {match.Username} (was {previous?.Username ?? "<none>"}); this is persisted");
    }
}

if (reconProfile is not null)
{
    if (GetArg("--server") is { } reconServer) reconProfile = reconProfile with { Server = reconServer };
    if (GetArg("--port") is { } reconPortStr && int.TryParse(reconPortStr, out var reconPort)) reconProfile = reconProfile with { Port = reconPort };

    // One-off command appended after the profile's own steps, so it runs once the bot is where the profile
    // takes it. For genuinely one-time actions that do not belong baked into a reusable profile.
    if (GetArg("--extra-cmd") is { } extraCmd)
    {
        reconProfile = reconProfile with { Steps = [.. reconProfile.Steps, new ReconStep(extraCmd, 3)] };
        Console.WriteLine($"[harness] one-off command appended after the profile steps: /{extraCmd}");
    }

    // Reference captures live with the source, not in bin/: they are checked-in data, not run artifacts.
    // Binary sits at <sln>/MinecraftProtoNet.ClaudeHarness/bin/<Cfg>/<tfm>, so four levels up is the solution root.
    var slnDir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent;
    var outRoot = GetArg("--out")
        ?? Path.Combine(slnDir?.FullName ?? AppContext.BaseDirectory, "_ServerReferences");

    var recon = new NpcReconTask(client, host.Services.GetRequiredService<IChatEventBus>(), baritoneProvider, outRoot);
    bool reconOk;
    try
    {
        reconOk = await recon.RunAsync(reconProfile);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[recon] EXCEPTION: {ex}");
        reconOk = false;
    }

    try { await gameLoop.StopAsync(); } catch { /* best-effort */ }
    Console.WriteLine($"[recon] DONE ok={reconOk}");
    return reconOk ? 0 : 1;
}

var runDir = Path.Combine(AppContext.BaseDirectory, "runs", $"{scenario.Name}-{DateTime.Now:yyyyMMdd-HHmmss}");
Directory.CreateDirectory(runDir);
Console.WriteLine($"[harness] run dir: {runDir}");

(int X, int Y, int Z)? diagPos = null;
if (GetArg("--diag") is { } diagStr)
{
    var parts = diagStr.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 3 && int.TryParse(parts[0], out var px) && int.TryParse(parts[1], out var py) && int.TryParse(parts[2], out var pz))
    {
        diagPos = (px, py, pz);
        Console.WriteLine($"[harness] DIAGNOSTIC mode: movement costs from ({px},{py},{pz})");
    }
}

var clearCourse = Array.IndexOf(args, "--clear-course") >= 0;
if (clearCourse) Console.WriteLine("[harness] --clear-course: this run resets the course to its default state");

// Build the course and exit without pathing, so it can be inspected/approved before the bot attempts it.
var buildOnly = Array.IndexOf(args, "--build-only") >= 0;

// Seconds to idle after settling at the start, before the goal is issued. Now 0 (off) by default.
// This briefly defaulted to 5 as a WORKAROUND: joining flooded the client faster than BouncyCastle's CFB8
// cipher could decrypt, so inbound confirmations arrived up to 1.6s late and the bot re-clicked gates it
// believed had not opened (gate1: 100 ticks, 51 right-click ticks, 5 bursts). Replacing that cipher with the
// BCL one fixed the backlog at source - unsettled is now 52 ticks, 4 click ticks, 2 bursts, with confirmation
// latency 89ms - so the workaround is no longer needed and would only hide a regression. Kept as a flag
// because deliberately settling is still useful when diagnosing join-time behaviour.
var settleSec = int.TryParse(GetArg("--settle-sec"), out var ss) ? ss : 0;
if (buildOnly) Console.WriteLine("[harness] --build-only: constructing the course, no run");

var controller = new RunController(
    client,
    baritoneProvider,
    gameLoop,
    host.Services.GetRequiredService<IContainerManager>(),
    registryService);
RunOutcome outcome;
try
{
    outcome = await controller.RunAsync(scenario, runDir, diagPos, clearCourse, buildOnly, settleSec);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[harness] EXCEPTION: {ex}");
    outcome = RunOutcome.Error;
}

if (MinecraftProtoNet.Baritone.Utils.MovementDiag.Lines.Count > 0)
{
    var diagPath = Path.Combine(runDir, "movement_diag.txt");
    await File.WriteAllLinesAsync(diagPath, MinecraftProtoNet.Baritone.Utils.MovementDiag.Lines);
    Console.WriteLine($"[harness] movement_diag.txt written ({MinecraftProtoNet.Baritone.Utils.MovementDiag.Lines.Count} lines)");
}

Console.WriteLine($"[harness] DONE outcome={outcome} dir={runDir}");
return outcome switch
{
    RunOutcome.Success => 0,
    RunOutcome.Error => 2,
    _ => 1 // Fall / CalcFail / Death / Stuck / Timeout
};
