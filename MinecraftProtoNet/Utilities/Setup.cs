using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Handlers.Meta;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Utilities;

public static class Setup
{
    public static void AddMinecraftClient(this IServiceCollection services)
    {
        services.AddSingleton<Connection>();
        //services.AddSingleton<MovementControllerNew>();
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();

        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
    }
}
