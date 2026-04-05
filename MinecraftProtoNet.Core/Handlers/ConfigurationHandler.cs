using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Configuration.Clientbound;
using MinecraftProtoNet.Core.Packets.Configuration.Serverbound;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.State.Base;
using SelectKnownPacksPacket = MinecraftProtoNet.Core.Packets.Configuration.Clientbound.SelectKnownPacksPacket;

namespace MinecraftProtoNet.Core.Handlers;

/// <summary>
/// Handles configuration phase packets including registry data, keep-alive, and state transitions.
/// </summary>
[HandlesPacket(typeof(SelectKnownPacksPacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.KeepAlivePacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.FinishConfigurationPacket))]
[HandlesPacket(typeof(RegistryDataPacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.CustomPayloadPacket))]
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

            case Packets.Configuration.Clientbound.CustomPayloadPacket customPayloadPacket:
                // Detect ViaVersion early during Configuration phase.
                // ViaVersion registers plugin channels like "vv:server_details" when proxying.
                if (customPayloadPacket.Channel is "vv:server_details" or "viaversion:config")
                {
                    client.State.ServerSettings.HasViaVersion = true;
                    logger.LogInformation("ViaVersion detected during configuration via channel: {Channel}",
                        customPayloadPacket.Channel);
                }
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

        // Initialize block tags first (used by BlockPhysicsData during block state loading)
        ClientState.InitializeBlockTags();

        // Initialize static registries from files
        await InitializeBlockStatesAsync();
        InitializeBiomesFromServerRegistry(client);
        await InitializeItemsAsync();
        await InitializeEntityTypesAsync();

        // ClientInformation is already sent early in LoginHandler (matching vanilla timing).
        // Signal configuration complete
        await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());

        // 3. Transition to Play state
        client.ProtocolState = ProtocolState.Play;
        logger.LogDebug("Protocol state changed to {State}", client.ProtocolState);

        // ChatSessionUpdate is sent after receiving the Login packet in PlayHandler,
        // matching vanilla's timing (ClientPacketListener.handleLogin → prepareKeyPair).
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

    private async Task InitializeEntityTypesAsync()
    {
        var entityTypes = await registryDataLoader.LoadEntityTypesAsync();
        ClientState.InitializeEntityTypeRegistry(entityTypes);
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

        // Store ordered key list for index-based lookups (e.g., DamageEventPacket.SourceTypeId)
        var orderedKeys = registryDataPacket.Tags.Keys.ToList();
        client.State.RegistryKeyOrder.AddOrUpdate(
            registryDataPacket.RegistryId,
            orderedKeys,
            (_, _) => orderedKeys);
    }
}
