using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Baritone.Pathfinding;
using MinecraftProtoNet.Baritone.Physics;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Baritone.Infrastructure;

/// <summary>
/// Extension methods for registering Baritone services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Baritone pathfinding and physics services.
    /// </summary>
    public static IServiceCollection AddBaritone(this IServiceCollection services)
    {
        services.AddSingleton<IPhysicsService, PhysicsService>();
        services.AddSingleton<IPathingService, PathingService>();
        return services;
    }
}
