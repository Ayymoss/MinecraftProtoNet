using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Core.Core.Abstractions;

namespace MinecraftProtoNet.Baritone.Utilities;

/// <summary>
/// Extension methods for registering Baritone services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Baritone services to the service collection and hooks them to the game loop.
    /// </summary>
    public static IServiceCollection AddBaritone(this IServiceCollection services)
    {
        // Register BaritoneProvider as singleton
        services.AddSingleton<IBaritoneProvider, BaritoneProvider>();
        
        // Hook Baritone to game loop after services are built
        services.AddSingleton<BaritoneGameLoopHook>();
        
        return services;
    }
    
    /// <summary>
    /// Service that hooks Baritone to the game loop during construction.
    /// </summary>
    internal class BaritoneGameLoopHook
    {
        public BaritoneGameLoopHook(IGameLoop gameLoop, ILogger<BaritoneGameLoopHook> logger)
        {
            // Hook Baritone tick events to the game loop
            BaritoneIntegration.HookToGameLoop(gameLoop, logger);
        }
    }
}

