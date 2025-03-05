using System.Text;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using Spectre.Console;

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
                    await client.SendPacketAsync(new Packets.Configuration.Serverbound.FinishConfigurationPacket());
                    client.State = ProtocolState.Play;
                    AnsiConsole.MarkupLine($"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.State.ToString()}[/]");
                    break;
            }
        }
    }
}
