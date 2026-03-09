namespace MinecraftProtoNet.Core.Configuration;

/// <summary>
/// Configuration for the humanization system. All timing ranges produce random values
/// between Min and Max (inclusive). Bound from appsettings.json "Humanizer" section.
/// </summary>
public sealed class HumanizerConfig
{
    public const string SectionName = "Humanizer";

    /// <summary>Master switch. When false, all humanization is bypassed (zero delays, no jitter).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Force humanization on when connected to a non-local server, even if Enabled=false.</summary>
    public bool ForceOnRemote { get; set; } = true;

    /// <summary>IP prefixes considered "local" (humanizer can be disabled). CIDR not parsed — prefix match only.</summary>
    public string[] LocalNetworks { get; set; } = ["127.0.0.1", "localhost", "10.10.1."];

    // --- Tick timing ---
    /// <summary>Minimum extra ms added to game loop tick sleep (can be negative for slight speedup).</summary>
    public int TickJitterMinMs { get; set; } = -1;
    /// <summary>Maximum extra ms added to game loop tick sleep.</summary>
    public int TickJitterMaxMs { get; set; } = 3;

    // --- Rotation noise on outgoing packets ---
    /// <summary>Max degrees of yaw/pitch noise added to position packets (symmetric ±).</summary>
    public float RotationJitterMaxDegrees { get; set; } = 0.04f;

    // --- GUI interaction timing ---
    /// <summary>Min delay between GUI slot clicks (ms).</summary>
    public int GuiClickMinMs { get; set; } = 100;
    /// <summary>Max delay between GUI slot clicks (ms).</summary>
    public int GuiClickMaxMs { get; set; } = 350;

    /// <summary>Min delay for GUI screen transitions — open, close, search (ms).</summary>
    public int GuiNavigationMinMs { get; set; } = 250;
    /// <summary>Max delay for GUI screen transitions (ms).</summary>
    public int GuiNavigationMaxMs { get; set; } = 900;

    // --- Chat command pacing ---
    /// <summary>Min delay before sending a chat command to the server (ms).</summary>
    public int ChatCommandMinMs { get; set; } = 500;
    /// <summary>Max delay before sending a chat command (ms).</summary>
    public int ChatCommandMaxMs { get; set; } = 1800;

    // --- Post-action pause ---
    /// <summary>Min pause after completing a significant action like a trade (ms).</summary>
    public int PostActionMinMs { get; set; } = 600;
    /// <summary>Max pause after completing a significant action (ms).</summary>
    public int PostActionMaxMs { get; set; } = 2500;

    // --- Idle behavior ---
    /// <summary>Minimum ticks between idle actions (look around, etc.).</summary>
    public int IdleMinIntervalTicks { get; set; } = 100;
    /// <summary>Maximum ticks between idle actions.</summary>
    public int IdleMaxIntervalTicks { get; set; } = 400;
    /// <summary>Max distance to search for entities to look at during idle (blocks).</summary>
    public double IdleLookMaxDistance { get; set; } = 20.0;

    // --- Chat safety on remote servers ---
    /// <summary>When true, non-slash messages are blocked on remote servers to prevent accidental chat leaks.</summary>
    public bool BlockNonCommandChatOnRemote { get; set; } = true;

    /// <summary>When true, ! bot commands from OTHER players' chat are ignored on remote servers.</summary>
    public bool BlockExternalCommandsOnRemote { get; set; } = true;
}
