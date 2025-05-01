using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace Bot;

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddMinecraftClient();
        var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IMinecraftClient>();
        var authResult = await client.AuthenticateAsync();
        if (!authResult)
        {
            Console.WriteLine("Authentication failed.");
            return;
        }

        await client.ConnectAsync("10.10.1.20", 25565);

        Console.ReadKey();
        await client.DisconnectAsync();
    }
}
