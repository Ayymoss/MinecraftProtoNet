using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Commands.Implementations;

[Command("placeit", Description = "Complex block placement routine")]
public class PlaceItCommand : ICommand
{
    public string Name => "placeit";
    public string Description => "Complex block placement routine (x y z)";
    public string[] Aliases => [];

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("Movement is disabled.");
    }
}
