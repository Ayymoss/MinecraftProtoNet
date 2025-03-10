using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;

namespace Bot;

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IMinecraftClient>();

        await client.ConnectAsync("10.10.1.20", 25565);


        //while (true)
        //{
        //    var coords = Console.ReadLine();
        //    if (coords?.Split(' ').Length != 3) continue;
        //    var coordsArray = coords.Split(' ').Select(float.Parse).ToArray();
        //    var targetPosition = new Vector3F(coordsArray[0], coordsArray[1], coordsArray[2]);
        //    await InterpolateToCoordinates(client, targetPosition);
//
        //    //var direction = Console.ReadKey();
        //    //await Move(client, direction.Key);
        //}


        //Console.WriteLine("Press Enter to disconnect...");
        Console.ReadKey();

        await client.DisconnectAsync();
    }

    // TODO: It should have an internal service collection
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Connection>();
        services.AddSingleton<ClientState>();
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();

        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
    }
}
