using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Play.Clientbound;
using MinecraftProtoNet.Services;

namespace MinecraftProtoNet.Handlers.Play;

/// <summary>
/// Handles time-related and world state packets.
/// </summary>
[HandlesPacket(typeof(SetTimePacket))]
[HandlesPacket(typeof(TickingStatePacket))]
[HandlesPacket(typeof(TickingStepPacket))]
[HandlesPacket(typeof(ServerDataPacket))]
public class TimeAndWorldHandler(ILogger<TimeAndWorldHandler> logger) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(TimeAndWorldHandler));

    public Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case SetTimePacket setTimePacket:
                client.State.Level.UpdateTickInformation(
                    setTimePacket.WorldAge,
                    setTimePacket.TimeOfDay,
                    setTimePacket.TimeOfDayIncreasing);
                break;

            case TickingStatePacket tickingStatePacket:
                logger.LogDebug("Ticking state: TickRate={TickRate}, Frozen={IsFrozen}",
                    tickingStatePacket.TickRate, tickingStatePacket.IsFrozen);
                break;

            case TickingStepPacket tickingStepPacket:
                logger.LogDebug("Ticking step: Steps={TickSteps}", tickingStepPacket.TickSteps);
                break;

            case ServerDataPacket serverDataPacket:
                logger.LogDebug("Server MOTD: {Motd}", serverDataPacket.Motd);
                break;
        }

        return Task.CompletedTask;
    }
}
