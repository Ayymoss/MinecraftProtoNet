using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Commands;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Handlers;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Utilities;

/// <summary>
/// Extension methods for registering Minecraft client services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Minecraft client services to the service collection.
    /// </summary>
    public static IServiceCollection AddMinecraftClient(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<Connection>();
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();
        
        // Game services
        services.AddSingleton<IPhysicsService, PhysicsService>();
        services.AddSingleton<IGameLoop, GameLoop>();
        
        // Data loading
        services.AddSingleton<IRegistryDataLoader, RegistryDataLoader>();
        
        // Command system
        services.AddSingleton<CommandRegistry>();
        
        // Packet handlers
        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
        
        // Logging - use the existing LoggingConfiguration
        services.AddSingleton<ILoggerFactory>(_ => LoggingConfiguration.CreateLoggerFactory());
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return services;
    }
}
