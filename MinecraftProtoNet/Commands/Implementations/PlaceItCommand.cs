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
        if (!ctx.State.LocalPlayer.HasEntity) return;

        if (!ctx.TryGetArg(0, out float x) ||
            !ctx.TryGetArg(1, out float y) ||
            !ctx.TryGetArg(2, out float z))
        {
            await ctx.SendUnsignedChatAsync("Usage: !placeit <x> <y> <z>");
            return;
        }

        var entity = ctx.State.LocalPlayer.Entity;
        if (entity.HeldItem.ItemId is null) return;

        await MovementActions.LookAtAsync(ctx, x, y, z, BlockFace.Top);
        await InteractionActions.PlaceBlockAsync(ctx);
        await MovementActions.LookAtAsync(ctx, x - 1, y, z, BlockFace.Top);
        await InteractionActions.PlaceBlockAsync(ctx);
        await MovementActions.LookAtAsync(ctx, x + 1, y, z, BlockFace.Top);
        await InteractionActions.PlaceBlockAsync(ctx);

        await Task.Delay(100);
        await MovementActions.LookAtAsync(ctx, x, y + 1, z, BlockFace.Top);
        await InteractionActions.PlaceBlockAsync(ctx);

        entity.StartJumping();
        await Task.Delay(100);
        entity.StopJumping();

        await MovementActions.LookAtAsync(ctx, x, y + 2, z, BlockFace.Top);
        await InteractionActions.PlaceBlockAsync(ctx);

        await Task.Delay(500);
        await MovementActions.LookAtAsync(ctx, x, y + 1, z, BlockFace.Bottom);

        var lookingAt = entity.GetLookingAtBlock(ctx.State.Level);
        if (lookingAt is null) return;

        await ctx.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.StartedDigging,
            Position = lookingAt.BlockPosition.ToVector3<int, double>(),
            Face = lookingAt.Face,
            Sequence = entity.IncrementSequence()
        });

        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(50);
            await InteractionActions.SwingHandAsync(ctx, Hand.MainHand);
        }

        await ctx.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.FinishedDigging,
            Position = lookingAt.BlockPosition.ToVector3<int, double>(),
            Face = lookingAt.Face,
            Sequence = entity.IncrementSequence()
        });
    }
}
