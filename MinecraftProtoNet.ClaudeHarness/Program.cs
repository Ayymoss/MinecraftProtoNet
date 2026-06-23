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

var client = host.Services.GetRequiredService<IMinecraftClient>();
var gameLoop = host.Services.GetRequiredService<IGameLoop>();
var baritoneProvider = host.Services.GetRequiredService<IBaritoneProvider>();

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

var controller = new RunController(client, baritoneProvider, gameLoop);
RunOutcome outcome;
try
{
    outcome = await controller.RunAsync(scenario, runDir, diagPos);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[harness] EXCEPTION: {ex}");
    outcome = RunOutcome.Error;
}

Console.WriteLine($"[harness] DONE outcome={outcome} dir={runDir}");
return outcome switch
{
    RunOutcome.Success => 0,
    RunOutcome.Error => 2,
    _ => 1 // Fall / CalcFail / Death / Stuck / Timeout
};
