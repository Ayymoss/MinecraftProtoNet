using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftProtoNet.Bazaar.Api;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Bazaar.Gui;
using MinecraftProtoNet.Bazaar.Services;
using MinecraftProtoNet.Core.Commands;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Bazaar.Commands;

/// <summary>
/// Chat command for testing and controlling the Bazaar trading system.
/// Usage: !bazaar <subcommand>
/// </summary>
[Command("bazaar", Aliases = ["bz"], Description = "Bazaar trading system controls")]
public class BazaarCommand(
    BazaarTradingEngine engine,
    IBazaarCompanionApi api,
    IContainerManager containerManager,
    IChatEventBus chatEventBus,
    ISignEventBus signEventBus,
    IOptions<BazaarTradingConfig> config,
    ILogger<BazaarCommand> logger) : ICommand
{
    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.HasMinArgs(1))
        {
            await ctx.SendChatAsync("Usage: !bazaar <status|start|stop|resume|api|container|chat|sign>");
            return;
        }

        var subcommand = ctx.Arguments[0].ToLowerInvariant();

        switch (subcommand)
        {
            case "status":
                await ShowStatusAsync(ctx);
                break;

            case "start":
                await StartEngineAsync(ctx);
                break;

            case "stop":
                engine.Stop();
                await ctx.SendChatAsync("[Bazaar] Engine stopped.");
                break;

            case "resume":
                engine.Resume();
                await ctx.SendChatAsync("[Bazaar] Engine resumed.");
                break;

            case "api":
                await TestApiAsync(ctx);
                break;

            case "container":
                await TestContainerAsync(ctx);
                break;

            case "chat":
                await TestChatAsync(ctx);
                break;

            case "sign":
                await TestSignAsync(ctx);
                break;

            default:
                await ctx.SendChatAsync($"[Bazaar] Unknown subcommand: {subcommand}");
                break;
        }
    }

    private async Task ShowStatusAsync(CommandContext ctx)
    {
        var cfg = config.Value;
        await ctx.SendChatAsync($"[Bazaar] Engine: {engine.State}");
        await ctx.SendChatAsync($"[Bazaar] API: {cfg.BazaarCompanionBaseUrl}");
        await ctx.SendChatAsync($"[Bazaar] Balance: {engine.FinancialState.CoinBalance:N0} | P&L: {engine.FinancialState.RealizedPnL:N0}");
        await ctx.SendChatAsync($"[Bazaar] Orders: {engine.Orders.ActiveOrders.Count} active | {engine.FinancialState.CompletedTradeCount} completed");

        if (engine.HaltReason is not null)
            await ctx.SendChatAsync($"[Bazaar] HALTED: {engine.HaltReason}");
    }

    private async Task StartEngineAsync(CommandContext ctx)
    {
        var balance = 0d;
        if (ctx.TryGetArg(1, out double bal))
            balance = bal;

        engine.Start(balance);
        await ctx.SendChatAsync($"[Bazaar] Engine started with balance {balance:N0}");
    }

    private async Task TestApiAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("[Bazaar] Testing BazaarCompanion API...");

        try
        {
            var health = await api.GetMarketHealthAsync();
            await ctx.SendChatAsync($"[Bazaar] API OK! Health: {health.HealthScore:F0} ({health.Recommendation})");
            await ctx.SendChatAsync($"[Bazaar] Products: {health.ActiveProductsCount} | Spread: {health.AverageSpread:F1}");

            var flips = await api.GetFlipOpportunitiesAsync(maxResults: 3);
            await ctx.SendChatAsync($"[Bazaar] Top {flips.Count} flips:");
            foreach (var flip in flips.Take(3))
            {
                await ctx.SendChatAsync($"  {flip.ProductKey}: bid {flip.BestBidPrice:F1} ask {flip.BestAskPrice:F1} score {flip.OpportunityScore:F1}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API test failed");
            await ctx.SendChatAsync($"[Bazaar] API ERROR: {ex.Message}");
        }
    }

    private async Task TestContainerAsync(CommandContext ctx)
    {
        if (!containerManager.IsContainerOpen)
        {
            await ctx.SendChatAsync("[Bazaar] No container open. Open a chest/GUI first.");
            return;
        }

        var container = containerManager.CurrentContainer;
        if (container is null)
        {
            await ctx.SendChatAsync("[Bazaar] Container is null.");
            return;
        }

        await ctx.SendChatAsync($"[Bazaar] Container open: {container.Slots.Count} slots");

        var count = 0;
        foreach (var (index, slot) in container.Slots)
        {
            if (slot.IsEmpty) continue;

            var name = BazaarGuiReader.GetItemName(slot);
            var lore = BazaarGuiReader.GetLoreLines(slot);
            var lorePreview = lore.Count > 0 ? $" | Lore[0]: {lore[0]}" : "";

            logger.LogInformation("[Bazaar] Slot {Index}: ItemId={ItemId} Count={Count} Name={Name}{Lore}",
                index, slot.ItemId, slot.ItemCount, name ?? "<no name>", lorePreview);

            if (name is not null)
            {
                await ctx.SendChatAsync($"  Slot {index}: {name} x{slot.ItemCount}{lorePreview}");
                count++;
            }

            if (count >= 10)
            {
                await ctx.SendChatAsync($"  ... and more (see logs for full dump)");
                break;
            }
        }

        if (count == 0)
            await ctx.SendChatAsync("[Bazaar] No named items found in container.");
    }

    private async Task TestChatAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("[Bazaar] Chat event bus test — listening for next system message...");

        var tcs = new TaskCompletionSource<SystemChatEventArgs>();
        void Handler(SystemChatEventArgs args) => tcs.TrySetResult(args);

        chatEventBus.OnSystemChat += Handler;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            cts.Token.Register(() => tcs.TrySetCanceled());

            var args = await tcs.Task;
            await ctx.SendChatAsync($"[Bazaar] Chat received! Translate: {args.TranslateKey ?? "null"} | Parts: {string.Join(" ", args.TextParts)}");
        }
        catch (TaskCanceledException)
        {
            await ctx.SendChatAsync("[Bazaar] No system chat received within 10s.");
        }
        finally
        {
            chatEventBus.OnSystemChat -= Handler;
        }
    }

    private async Task TestSignAsync(CommandContext ctx)
    {
        await ctx.SendChatAsync("[Bazaar] Sign event bus test — listening for sign editor open...");

        var tcs = new TaskCompletionSource<SignEditorEventArgs>();
        Task Handler(SignEditorEventArgs args)
        {
            tcs.TrySetResult(args);
            return Task.CompletedTask;
        }

        signEventBus.OnSignEditorOpened += Handler;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => tcs.TrySetCanceled());

            var args = await tcs.Task;
            await ctx.SendChatAsync($"[Bazaar] Sign editor opened at {args.Position} (front={args.IsFrontText})");
        }
        catch (TaskCanceledException)
        {
            await ctx.SendChatAsync("[Bazaar] No sign editor opened within 30s. Place and right-click a sign to test.");
        }
        finally
        {
            signEventBus.OnSignEditorOpened -= Handler;
        }
    }
}
