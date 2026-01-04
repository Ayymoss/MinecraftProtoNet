using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("placeit", Description = "Complex block placement routine")]
public class PlaceItCommand : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
