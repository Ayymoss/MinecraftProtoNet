using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("cmd", Description = "Execute a server command")]
public class CmdCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendChatAsync("Usage: !cmd <command>");
            return;
        }

        var command = ctx.GetRemainingArgsAsString(0);
        await ctx.SendPacketAsync(new ChatCommandPacket(command));
    }
}
