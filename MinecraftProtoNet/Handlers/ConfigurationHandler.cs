using System.Text.Json;
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

namespace MinecraftProtoNet.Handlers
{
    public class ConfigurationHandler : IPacketHandler
    {
        public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        [
            (ProtocolState.Configuration, 0x01),
            (ProtocolState.Configuration, 0x02),
            (ProtocolState.Configuration, 0x03),
            (ProtocolState.Configuration, 0x04),
            (ProtocolState.Configuration, 0x07),
            (ProtocolState.Configuration, 0x0C),
            (ProtocolState.Configuration, 0x0D),
            (ProtocolState.Configuration, 0x0E),
        ];

        public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
        {
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
                    // Setup the client environment
                    var blockJsonFilePath = Path.Combine(AppContext.BaseDirectory, "StaticFiles", "blocks-1.21.4.json"); // TODO: Rehome
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
                        Path.Combine(AppContext.BaseDirectory, "StaticFiles", "registries-1.21.4.json"); // TODO: Rehome
                    var registryJsonString = await File.ReadAllTextAsync(registryJsonFilePath);
                    var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryRoot>>(registryJsonString) ?? [];
                    var itemData = registry["minecraft:item"].Entries
                        .ToDictionary(x => x.Value.ProtocolId, x => x.Key);
                    ClientState.InitialiseItemRegistry(itemData);

                    // Continue to play.
                    await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());
                    client.ProtocolState = ProtocolState.Play;
                    AnsiConsole.MarkupLine(
                        $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.ProtocolState.ToString()}[/]");
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
}
