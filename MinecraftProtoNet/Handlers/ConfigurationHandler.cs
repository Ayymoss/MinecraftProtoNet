using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using MinecraftProtoNet.Packets.Configuration.Serverbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;
using SelectKnownPacksPacket = MinecraftProtoNet.Packets.Configuration.Clientbound.SelectKnownPacksPacket;

namespace MinecraftProtoNet.Handlers;

/// <summary>
/// Handles configuration phase packets including registry data, keep-alive, and state transitions.
/// </summary>
[HandlesPacket(typeof(SelectKnownPacksPacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.KeepAlivePacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.FinishConfigurationPacket))]
[HandlesPacket(typeof(RegistryDataPacket))]
public class ConfigurationHandler(
    ILogger<ConfigurationHandler> logger,
    IRegistryDataLoader registryDataLoader) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ConfigurationHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case SelectKnownPacksPacket:
                await HandleSelectKnownPacksAsync(client);
                break;

            case Packets.Configuration.Clientbound.KeepAlivePacket keepAlivePacket:
                await HandleKeepAliveAsync(client, keepAlivePacket);
                break;

            case Packets.Configuration.Clientbound.FinishConfigurationPacket:
                await HandleFinishConfigurationAsync(client);
                break;

            case RegistryDataPacket registryDataPacket:
                HandleRegistryData(client, registryDataPacket);
                break;
        }
    }

    private static async Task HandleSelectKnownPacksAsync(IMinecraftClient client)
    {
        await client.SendPacketAsync(new Packets.Configuration.Serverbound.SelectKnownPacksPacket
        {
            KnownPacks = []
        });
    }

    private static async Task HandleKeepAliveAsync(
        IMinecraftClient client,
        Packets.Configuration.Clientbound.KeepAlivePacket keepAlivePacket)
    {
        await client.SendPacketAsync(new Packets.Configuration.Serverbound.KeepAlivePacket
        {
            Payload = keepAlivePacket.Payload
        });
    }

    private async Task HandleFinishConfigurationAsync(IMinecraftClient client)
    {
        logger.LogDebug("Finishing configuration phase...");

        // Initialize static registries from files
        await InitializeBlockStatesAsync();
        InitializeBiomesFromServerRegistry(client);
        await InitializeItemsAsync();

        // 1. Send client information (required during configuration)
        await client.SendPacketAsync(new ClientInformationPacket());

        // 2. Signal configuration complete
        await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());

        // 3. Transition to Play state
        client.ProtocolState = ProtocolState.Play;
        logger.LogDebug("Protocol state changed to {State}", client.ProtocolState);

        // 4. Send chat session update (Play state packet)
        await client.SendChatSessionUpdate();
    }

    private async Task InitializeBlockStatesAsync()
    {
        var blockStates = await registryDataLoader.LoadBlockStatesAsync();
        ClientState.InitializeBlockStateRegistry(blockStates);
    }

    private static void InitializeBiomesFromServerRegistry(IMinecraftClient client)
    {
        var biomes = client.State.Registry["minecraft:worldgen/biome"]
            .Select((x, index) => new { Index = index, x.Key })
            .ToDictionary(k => k.Index, v => new Biome(v.Index, v.Key));
        ClientState.InitializeBiomeRegistry(biomes);
    }

    private async Task InitializeItemsAsync()
    {
        var items = await registryDataLoader.LoadItemsAsync();
        ClientState.InitialiseItemRegistry(items);
    }

    private static void HandleRegistryData(IMinecraftClient client, RegistryDataPacket registryDataPacket)
    {
        client.State.Registry.AddOrUpdate(
            registryDataPacket.RegistryId,
            registryDataPacket.Tags,
            (_, existingTags) =>
            {
                foreach (var tag in registryDataPacket.Tags)
                {
                    existingTags[tag.Key] = tag.Value;
                }

                return existingTags;
            });
    }
}
