using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("ping", Description = "Send a ping request to the server")]
public class PingCommand : ICommand
{
    public string Name => "ping";
    public string Description => "Send a ping request to the server";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendPacketAsync(new PingRequestPacket
        {
            Payload = TimeProvider.System.GetLocalNow().ToUnixTimeMilliseconds()
        });
    }
}
