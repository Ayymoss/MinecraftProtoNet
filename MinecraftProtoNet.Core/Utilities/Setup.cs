using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Actions;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Utilities;

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
        // Shared state (must be registered before services that depend on it)
        services.AddSingleton<ClientState>();

        // Core services
        services.AddSingleton<Connection>();
        services.AddSingleton<IPacketSender>(sp => sp.GetRequiredService<Connection>());
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();

        // Game services
        services.AddSingleton<IClientStateAccessor, ClientStateAccessor>();
        services.AddSingleton<IGameLoop, GameLoop>();
        services.AddSingleton<IPhysicsService, PhysicsService>();

        // Data loading
        services.AddSingleton<IRegistryDataLoader, RegistryDataLoader>();
        services.AddSingleton<IItemRegistryService, ItemRegistryService>();
        
        // Interaction Services
        services.AddSingleton<IInventoryManager, InventoryManager>();
        services.AddSingleton<IContainerManager, ContainerManager>();
        services.AddSingleton<IInteractionManager, InteractionManager>();

        // Command system
        services.AddSingleton<CommandRegistry>();

        // Packet handlers
        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
        
        // Specialized Play Handlers
        services.AddSingleton<IPacketHandler, Handlers.Play.ChatHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.ChunkHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.ConnectionHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.EntityHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.InventoryHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.ContainerHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.PlayerInfoHandler>();
        services.AddSingleton<IPacketHandler, Handlers.Play.TimeAndWorldHandler>();

        // Logging - use the existing LoggingConfiguration
        services.AddSingleton<ILoggerFactory>(_ => LoggingConfiguration.CreateLoggerFactory());
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return services;
    }
}
