using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace MinecraftProtoNet.Core;

/// <summary>
/// Configures Serilog as the logging provider with console and file sinks.
/// </summary>
public static class LoggingConfiguration
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly object Lock = new();

    /// <summary>
    /// Creates or returns the shared logger factory configured with Serilog.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory()
    {
        if (_loggerFactory is not null)
            return _loggerFactory;

        lock (Lock)
        {
            if (_loggerFactory is not null)
                return _loggerFactory;

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/minecraft-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .CreateLogger();

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
