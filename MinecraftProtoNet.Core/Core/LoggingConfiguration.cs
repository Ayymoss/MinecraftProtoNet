using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace MinecraftProtoNet.Core.Core;

/// <summary>
/// Configures Serilog as the logging provider with console and file sinks.
/// </summary>
public static class LoggingConfiguration
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly Lock Lock = new();

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
            var logPath = Path.Combine(binPath, "logs", "logicstream-.log");

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
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
}
