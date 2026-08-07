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
[HandlesPacket(typeof(Packets.Configuration.Clientbound.ResourcePackPushPacket))]
[HandlesPacket(typeof(Packets.Configuration.Clientbound.ResourcePackPopPacket))]
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

            case Packets.Configuration.Clientbound.ResourcePackPushPacket resourcePackPush:
                await HandleResourcePackPushAsync(client, resourcePackPush);
                break;

            case Packets.Configuration.Clientbound.ResourcePackPopPacket resourcePackPop:
                logger.LogDebug("Resource pack popped during configuration: PackId={PackId}",
                    resourcePackPop.PackId?.ToString() ?? "<all>");
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

    /// <summary>
    /// Answers a configuration-phase resource pack push by accepting it.
    ///
    /// Vanilla flow: accept → download → apply, reporting each stage.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/multiplayer/ClientCommonPacketListenerImpl.java
    ///
    /// The three stages are reported back-to-back with no simulated download delay, unlike the Play-state
    /// handler. Configuration packets are processed on the network/keep-alive path, and a server that pushes a
    /// pack here is *blocking* the handshake on our reply: sleeping in this method would stall keep-alive
    /// responses and risk a timeout kick, to buy realism the server cannot act on at this point anyway.
    ///
    /// Declining is not an option in general — a pack marked Required disconnects clients that refuse.
    /// </summary>
    private async Task HandleResourcePackPushAsync(
        IMinecraftClient client,
        Packets.Configuration.Clientbound.ResourcePackPushPacket push)
    {
        logger.LogInformation(
            "Resource pack push during configuration: PackId={PackId}, Url={Url}, Required={Required}",
            push.PackId, push.Url, push.Required);

        foreach (var action in new[]
                 {
                     Packets.Play.Serverbound.ResourcePackPacket.ResourcePackAction.Accepted,
                     Packets.Play.Serverbound.ResourcePackPacket.ResourcePackAction.Downloaded,
                     Packets.Play.Serverbound.ResourcePackPacket.ResourcePackAction.SuccessfullyLoaded
                 })
        {
            await client.SendPacketAsync(new Packets.Configuration.Serverbound.ResourcePackPacket
            {
                PackId = push.PackId,
                Action = action
            });
        }

        logger.LogInformation("Resource pack accepted and reported loaded: PackId={PackId}", push.PackId);
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
        await InitializeAttributesAsync();
        InitializeEnchantmentsFromServerRegistry(client);
        InitializeMobEffectsFromServerRegistry(client);

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

        var blockDefinitions = await registryDataLoader.LoadBlockDefinitionsAsync();
        ClientState.InitializeBlockDefinitions(blockDefinitions);

        var blockHardness = await registryDataLoader.LoadBlockHardnessAsync();
        ClientState.InitializeBlockHardness(blockHardness);
    }

    private static void InitializeEnchantmentsFromServerRegistry(IMinecraftClient client)
    {
        // The enchantment registry's ordered keys give each enchant its holder index — the key used in an
        // item's Enchantments component map. Sent during configuration like any synced datapack registry.
        if (client.State.RegistryKeyOrder.TryGetValue("minecraft:enchantment", out var orderedKeys))
        {
            var map = new Dictionary<string, int>(orderedKeys.Count);
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                map[orderedKeys[i]] = i;
            }
            ClientState.InitializeEnchantmentRegistry(map);
        }
    }

    private static void InitializeMobEffectsFromServerRegistry(IMinecraftClient client)
    {
        // mob_effect registry's ordered keys give each effect its holder index (the id in UpdateMobEffect packets).
        if (client.State.RegistryKeyOrder.TryGetValue("minecraft:mob_effect", out var orderedKeys))
        {
            var map = new Dictionary<string, int>(orderedKeys.Count);
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                map[orderedKeys[i]] = i;
            }
            ClientState.InitializeMobEffectRegistry(map);
        }
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

    private async Task InitializeAttributesAsync()
    {
        var attributes = await registryDataLoader.LoadAttributesAsync();
        ClientState.InitializeAttributeRegistry(attributes);
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
