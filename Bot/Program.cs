using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Services;

namespace Bot;

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IMinecraftClient>();

        await client.ConnectAsync("10.10.1.20", 25555);

        //Console.WriteLine("Press Enter to disconnect...");
        Console.ReadKey();

        await client.DisconnectAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Connection>();
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();

        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
    }
}
