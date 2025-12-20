using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;

namespace Bot;

// TODO: We need to code-gen for blocks, registries etc

/*
 * IMPORTANT: USE THE `minecraft-26.1-REFERENCE-ONLY` as the foundational truth lookup as it is the client/server sourcecode. Everything from the Java client will be there. We just need to resolve the C#-implementation for the same components.
 */
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

        await client.ConnectAsync("10.10.1.20", 25565, true);

        Console.ReadKey();
        await client.DisconnectAsync();
    }
}
