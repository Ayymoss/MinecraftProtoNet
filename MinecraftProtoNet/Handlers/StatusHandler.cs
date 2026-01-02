using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<StatusHandler> _logger = LoggingConfiguration.CreateLogger<StatusHandler>();

    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(StatusHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case StatusResponsePacket statusResponse:
                _logger.LogInformation("Client Protocol: {ProtocolVersion}", client.ProtocolVersion);
                _logger.LogInformation("Status Response: {Response}", statusResponse.Response);

                try
                {
                    var document = JsonDocument.Parse(statusResponse.Response);
                    var version = document.RootElement.GetProperty("version");
                    client.ProtocolVersion = version.GetProperty("protocol").GetInt32();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse status response");
                }

                await client.SendPacketAsync(new PingRequestPacket
                    { Payload = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() });
                break;
            case PongResponsePacket pong:
                _logger.LogInformation("Ping: {PingMs}ms", TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() - pong.Payload);
                _logger.LogInformation("Client Protocol: {ProtocolVersion}", client.ProtocolVersion);
                break;
        }
    }
}
