using System.Text.Json;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Status.Clientbound;
using MinecraftProtoNet.Packets.Status.Serverbound;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Handlers;

[HandlesPacket(typeof(StatusResponsePacket))]
[HandlesPacket(typeof(PongResponsePacket))]
public class StatusHandler : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(StatusHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case StatusResponsePacket statusResponse:
                Console.WriteLine($"Client Protocol: {client.ProtocolVersion}");
                Console.WriteLine($"Status Response: {statusResponse.Response}");

                try
                {
                    var document = JsonDocument.Parse(statusResponse.Response);
                    var version = document.RootElement.GetProperty("version");
                    client.ProtocolVersion = version.GetProperty("protocol").GetInt32();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                await client.SendPacketAsync(new PingRequestPacket
                    { Payload = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() });
                break;
            case PongResponsePacket pong:
                Console.WriteLine($"Ping: {TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() - pong.Payload}ms");
                Console.WriteLine($"Client Protocol: {client.ProtocolVersion}");
                break;
        }
    }
}
