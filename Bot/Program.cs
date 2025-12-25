using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Utilities;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State;

namespace Bot;

// TODO: We need to code-gen for blocks, registries etc

/*
<IMPORTANT START>
USE THE `minecraft-26.1-REFERENCE-ONLY` as the foundational truth lookup as it is the client/server sourcecode, packets to protocol implementation specifics etc. Everything from the Java client will be there. We just need to resolve the C#-implementation for the same components.
USE THE `baritone-1.21.8-REFERENCE-ONLY` as the foundational truth lookup for pathfinding and bot logic. Baritone is the de-facto standard for Minecraft pathfinding and botting, and has a lot of the logic we need already implemented.
</IMPORTANT END>
 */
public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        
        services.AddMinecraftClient();
        var serviceProvider = services.BuildServiceProvider();

        // Initialize registry service
        var registryService = serviceProvider.GetRequiredService<IItemRegistryService>();
        await registryService.InitializeAsync();
        
        // Set static registry in EntityInventory
        EntityInventory.SetRegistryService(registryService);

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
