using System.Text;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using MinecraftProtoNet.State.Base;
using Spectre.Console;

namespace MinecraftProtoNet.Handlers
{
    public class ConfigurationHandler(ClientState clientState) : IPacketHandler
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
                    ClientState.InitializeBlockStateRegistry(new Dictionary<int, BlockState>
                    {
                        [0] = BlockState.Air,
                        [85] = new(85, "minecraft:bedrock"),
                        [27579] = new(27579, "minecraft:verdant_froglight"),
                        [27581] = new(27581, "minecraft:unknown"),
                        [27584] = new(27584, "minecraft:unknown"),
                        [27578] = new(27578, "minecraft:unknown"),
                        [27576] = new(27576, "minecraft:ochre_froglight"),
                        [27582] = new(27582, "minecraft:pearlescent_froglight"),
                    });

                    var biomes = clientState.Registry["minecraft:worldgen/biome"]
                        .Select((x, index) => new { i = index, x.Key })
                        .ToDictionary(k => k.i, v => new Biome(v.i, v.Key));
                    ClientState.InitializeBiomeRegistry(biomes);

                    // Continue to play.
                    await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());
                    client.State = ProtocolState.Play;
                    AnsiConsole.MarkupLine(
                        $"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.State.ToString()}[/]");
                    break;
                }
                case RegistryDataPacket registryDataPacket:
                    clientState.Registry.AddOrUpdate(registryDataPacket.RegistryId, registryDataPacket.Tags,
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
