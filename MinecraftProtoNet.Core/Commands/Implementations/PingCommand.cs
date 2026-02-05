using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core.Commands.Implementations;

[Command("ping", Description = "Send a ping request to the server")]
public class PingCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendPacketAsync(new PingRequestPacket
        {
            Payload = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds()
        });
    }
}
