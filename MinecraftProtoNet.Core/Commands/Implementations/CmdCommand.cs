using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Commands.Implementations;

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
        if (command.StartsWith('/'))
        {
            command = command[1..];
        }

        if (ctx.State.ServerSettings.EnforcesSecureChat && ctx.AuthResult is not null)
        {
            var packet = ChatSigning.CreateSignedChatCommandPacket(ctx.AuthResult, command);
            if (packet != null)
            {
                await ctx.SendPacketAsync(packet);
                return;
            }
        }

        await ctx.SendPacketAsync(new ChatCommandPacket(command));
    }
}
