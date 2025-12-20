using System.Text.Json;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Json;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.State.Base;
using Spectre.Console;
using BlockState = MinecraftProtoNet.Models.World.Chunk.BlockState;

namespace MinecraftProtoNet.Handlers;

[HandlesPacket(typeof(SelectKnownPacksPacket))]
[HandlesPacket(typeof(KeepAlivePacket))]
[HandlesPacket(typeof(FinishConfigurationPacket))]
[HandlesPacket(typeof(RegistryDataPacket))]
public class ConfigurationHandler : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(ConfigurationHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        Console.WriteLine($"[DEBUG] ConfigurationHandler handling packet: {packet.GetType().Name}");
        switch (packet)
        {
            case SelectKnownPacksPacket selectKnownPacksPacket:
                await client.SendPacketAsync(new Packets.Configuration.Serverbound.SelectKnownPacksPacket { KnownPacks = [] });
                break;
            case KeepAlivePacket keepAlivePacket:
                await client.SendPacketAsync(
                    new Packets.Configuration.Serverbound.KeepAlivePacket { Payload = keepAlivePacket.Payload });
                break;
            case FinishConfigurationPacket finishConfigurationPacket:
            {
                Console.WriteLine("[DEBUG] Handling FinishConfigurationPacket...");
                // Setup the client environment
                var blockJsonFilePath = Path.Combine(AppContext.BaseDirectory, "StaticFiles", "blocks-26.1.json"); // TODO: Rehome
                var blockJsonString = await File.ReadAllTextAsync(blockJsonFilePath);
                var blockData = JsonSerializer.Deserialize<Dictionary<string, BlockRoot>>(blockJsonString) ?? [];
                var blockStateData = blockData
                    .SelectMany(kvp => kvp.Value.States.Select(state => new { BlockName = kvp.Key, StateId = state.Id }))
                    .ToDictionary(x => x.StateId, x => new BlockState(x.StateId, x.BlockName));
                ClientState.InitializeBlockStateRegistry(blockStateData);

                var biomes = client.State.Registry["minecraft:worldgen/biome"]
                    .Select((x, index) => new { i = index, x.Key })
                    .ToDictionary(k => k.i, v => new Biome(v.i, v.Key));
                ClientState.InitializeBiomeRegistry(biomes);

                var registryJsonFilePath =
                    Path.Combine(AppContext.BaseDirectory, "StaticFiles", "registries-26.1.json"); // TODO: Rehome
                var registryJsonString = await File.ReadAllTextAsync(registryJsonFilePath);
                var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryRoot>>(registryJsonString) ?? [];
                var itemData = registry["minecraft:item"].Entries
                    .ToDictionary(x => x.Value.ProtocolId, x => x.Key);
                ClientState.InitialiseItemRegistry(itemData);

                // Correct Order:
                // 1. Send ClientInformation (Mandatory in Config)
                await client.SendPacketAsync(new Packets.Configuration.Serverbound.ClientInformationPacket());
                
                // 2. Send FinishConfiguration (Signals end of Config)
                await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());
                
                // 3. Switch to Play State
                client.ProtocolState = ProtocolState.Play;
                AnsiConsole.MarkupLine(
                    $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.ProtocolState.ToString()}[/]");
                
                // 4. Send ChatSessionUpdate (This is a PLAY packet, must be sent after state switch)
                await client.SendChatSessionUpdate();
                break;
            }
            case RegistryDataPacket registryDataPacket:
                client.State.Registry.AddOrUpdate(registryDataPacket.RegistryId, registryDataPacket.Tags,
                    (registryId, existingTags) =>
                    {
                        foreach (var tagPair in registryDataPacket.Tags) existingTags[tagPair.Key] = tagPair.Value;
                        return existingTags;
                    });
                break;
        }
    }
}
