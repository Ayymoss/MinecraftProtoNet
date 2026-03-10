using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace MinecraftProtoNet.Core.Core;

/// <summary>
/// Configures Serilog as the logging provider with console and file sinks.
/// Console output uses short class names with color-coded category tags for quick visual scanning,
/// while preserving Serilog's native structured value highlighting (strings, numbers, objects).
/// </summary>
public static class LoggingConfiguration
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly Lock Lock = new();

    // Namespace-to-tag mapping for visual categorization in console output.
    // Tags are 3 chars for consistent alignment.
    private static readonly (string Prefix, string Tag)[] CategoryTags =
    [
        ("MinecraftProtoNet.Core.Core.Connection",        "NET"),
        ("MinecraftProtoNet.Core.Services.PhysicsService", "PHY"),
        ("MinecraftProtoNet.Core.Services.GameLoop",       "TIK"),
        ("MinecraftProtoNet.Core.Handlers.Play.Chat",      "CHT"),
        ("MinecraftProtoNet.Core.Services.Humanizer",      "HMN"),
        ("MinecraftProtoNet.Core.Services.DefaultChatSink", "CHT"),
        ("MinecraftProtoNet.Core.Services.WebcoreChatSink", "CHT"),
        ("MinecraftProtoNet.Core.Services.Container",      "GUI"),
        ("MinecraftProtoNet.Core.Core.MinecraftClient",    "BOT"),
        ("MinecraftProtoNet.Core.Commands",                "CMD"),
        ("MinecraftProtoNet.Core.Auth",                    "AUT"),
        ("MinecraftProtoNet.Core.Handlers",                "HDL"),
        ("MinecraftProtoNet.Bazaar",                       "BZR"),
        ("MinecraftProtoNet.Baritone",                     "BAR"),
    ];

    /// <summary>
    /// Creates or returns the shared logger factory configured with Serilog.
    /// </summary>
    /// <param name="minLevel">Minimum log level (default: Debug). Use Verbose for detailed tick-by-tick logs.</param>
    public static ILoggerFactory CreateLoggerFactory(LogEventLevel minLevel = LogEventLevel.Debug)
    {
        if (_loggerFactory is not null)
        {
            return _loggerFactory;
        }

        lock (Lock)
        {
            var binPath = AppDomain.CurrentDomain.BaseDirectory;
            var logPath = Path.Combine(binPath, "logs", "minecraftProtoNet-.log");

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                // Reduce noise from high-frequency components at Debug level
                .MinimumLevel.Override("MinecraftProtoNet.Core.Core.Connection", LogEventLevel.Information)
                .MinimumLevel.Override("MinecraftProtoNet.Core.Services.PhysicsService", LogEventLevel.Information)
                .MinimumLevel.Override("MinecraftProtoNet.Core.Services.GameLoop", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With<ShortContextEnricher>()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CategoryTag} {ShortContext,-20} {Message:lj}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code)
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Set the static logger for classes using Log.Verbose(), Log.Debug(), etc.
            Log.Logger = serilogLogger;

            _loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
            return _loggerFactory;
        }
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger<T> CreateLogger<T>()
    {
        return CreateLoggerFactory().CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return CreateLoggerFactory().CreateLogger(categoryName);
    }

    /// <summary>
    /// Enricher that adds ShortContext (class name only) and CategoryTag (3-char subsystem label)
    /// as scalar properties for use in the output template. The actual message rendering is left
    /// to Serilog's console theme so structured values (strings, numbers, objects) get highlighted.
    /// </summary>
    private sealed class ShortContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var sourceContext = logEvent.Properties.TryGetValue("SourceContext", out var sc)
                ? sc.ToString().Trim('"')
                : "";

            var className = ExtractClassName(sourceContext);
            var tag = GetCategoryTag(sourceContext);

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ShortContext", className));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CategoryTag", tag));
        }

        private static string ExtractClassName(string sourceContext)
        {
            if (string.IsNullOrEmpty(sourceContext)) return "System";
            var lastDot = sourceContext.LastIndexOf('.');
            return lastDot >= 0 ? sourceContext[(lastDot + 1)..] : sourceContext;
        }

        private static string GetCategoryTag(string sourceContext)
        {
            foreach (var (prefix, tag) in CategoryTags)
            {
                if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
                    return tag;
            }

            return "---";
        }
    }
}
