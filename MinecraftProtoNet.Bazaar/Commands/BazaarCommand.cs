using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftProtoNet.Bazaar.Api;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Bazaar.Gui;
using MinecraftProtoNet.Bazaar.Orders;
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
            await ctx.SendChatAsync("Usage: !bazaar <status|start|stop|resume|simulate|api|container|chat|sign>");
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

            case "simulate":
            case "sim":
                await SimulateAsync(ctx);
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

    /// <summary>
    /// Simulates a full trading lifecycle with synthetic data.
    /// All progress is logged locally — only a brief summary is sent to chat to avoid spam kicks.
    /// Usage: !bz simulate [balance] [trades]
    /// </summary>
    private async Task SimulateAsync(CommandContext ctx)
    {
        var balance = 1_000_000d;
        var tradeCount = 3;

        if (ctx.TryGetArg(1, out double bal))
            balance = bal;
        if (ctx.TryGetArg(2, out int count))
            tradeCount = Math.Clamp(count, 1, 10);

        logger.LogInformation("[Sim] Starting simulation: {Balance:N0} coins, {Trades} trades", balance, tradeCount);

        // Start engine if not running
        if (!engine.IsRunning)
            engine.Start(balance);
        else
            engine.FinancialState.CoinBalance = balance;

        // Synthetic flip opportunities
        var products = new[]
        {
            ("ENCHANTED_DIAMOND", "Enchanted Diamond", 1_280.0, 1_340.0, 8.5),
            ("ENCHANTED_GOLD_BLOCK", "Enchanted Gold Block", 25_600.0, 26_200.0, 7.2),
            ("ENCHANTED_IRON_BLOCK", "Enchanted Iron Block", 3_200.0, 3_350.0, 6.8),
            ("ENCHANTED_LAPIS_LAZULI", "Enchanted Lapis Lazuli", 640.0, 680.0, 5.5),
            ("ENCHANTED_REDSTONE_BLOCK", "Enchanted Redstone Block", 12_800.0, 13_100.0, 4.9),
            ("ENCHANTED_EMERALD", "Enchanted Emerald", 5_120.0, 5_280.0, 4.3),
            ("HOT_POTATO_BOOK", "Hot Potato Book", 80_000.0, 82_500.0, 3.8),
            ("FUMING_POTATO_BOOK", "Fuming Potato Book", 1_500_000.0, 1_540_000.0, 3.2),
            ("RECOMBOBULATOR_3000", "Recombobulator 3000", 5_500_000.0, 5_650_000.0, 2.9),
            ("BOOSTER_COOKIE", "Booster Cookie", 1_800_000.0, 1_850_000.0, 2.5),
        };

        var cfg = config.Value;
        var rng = new Random(42);
        var completedTrades = 0;
        var totalProfit = 0d;

        for (var t = 0; t < tradeCount && t < products.Length; t++)
        {
            var (key, name, bid, ask, score) = products[t];

            // Check affordability
            var maxQty = (int)(cfg.MaxPerTradeInvestment / bid);
            var qty = Math.Max(1, Math.Min(maxQty, 64));
            var totalCost = bid * qty;

            if (totalCost > engine.FinancialState.CoinBalance)
            {
                logger.LogInformation("[Sim] Skipping {Name}: insufficient balance ({Cost:N0} > {Balance:N0})",
                    name, totalCost, engine.FinancialState.CoinBalance);
                continue;
            }

            // Phase 1: Place buy order
            var orderId = $"sim-buy-{key}-{DateTime.UtcNow.Ticks}";
            var buyOrder = new OrderRecord
            {
                Id = orderId,
                ProductKey = key,
                ProductName = name,
                Side = OrderSide.Buy,
                PricePerUnit = bid,
                Quantity = qty,
                OriginalPrice = bid,
                Status = OrderStatus.Active
            };
            engine.Orders.AddOrder(buyOrder);
            engine.FinancialState.RecordInvestment(totalCost);

            logger.LogInformation("[Sim] BUY {Qty}x {Name} @ {Bid:N1} = {Cost:N0}", qty, name, bid, totalCost);

            // Phase 2: Simulate undercut + walk (50% chance)
            if (rng.NextDouble() > 0.5)
            {
                var undercutBy = bid * 0.005; // 0.5% undercut
                var newBid = bid + undercutBy;
                var profit = (ask * (1 - cfg.TaxRate)) - newBid;

                if (profit > cfg.MinProfitPerUnit)
                {
                    var oldPrice = buyOrder.PricePerUnit;
                    buyOrder.PricePerUnit = Math.Round(newBid, 1);
                    buyOrder.WalkCount++;
                    logger.LogInformation("[Sim] WALK #{Walk}: {Old:N1} -> {New:N1} (undercut)",
                        buyOrder.WalkCount, oldPrice, buyOrder.PricePerUnit);
                }
            }

            // Phase 3: Simulate buy fill
            engine.Orders.UpdateStatus(orderId, OrderStatus.Filled);

            // Phase 4: Place sell offer
            var sellId = $"sim-sell-{key}-{DateTime.UtcNow.Ticks}";
            var sellOrder = new OrderRecord
            {
                Id = sellId,
                ProductKey = key,
                ProductName = name,
                Side = OrderSide.Sell,
                PricePerUnit = ask,
                Quantity = qty,
                OriginalPrice = ask,
                Status = OrderStatus.Active
            };
            engine.Orders.AddOrder(sellOrder);

            // Phase 5: Simulate sell fill + complete trade
            engine.Orders.UpdateStatus(sellId, OrderStatus.Filled);

            var trade = new TradeRecord(
                ProductKey: key,
                ProductName: name,
                Quantity: qty,
                BuyPricePerUnit: buyOrder.PricePerUnit,
                SellPricePerUnit: ask,
                TaxRate: cfg.TaxRate,
                CompletedAt: DateTime.UtcNow);
            engine.FinancialState.RecordCompletedTrade(trade);

            logger.LogInformation("[Sim] COMPLETE {Name}: {Qty}x buy@{Buy:N1} sell@{Sell:N1} profit={Profit:N0} ({Pct:F1}%)",
                name, qty, buyOrder.PricePerUnit, ask, trade.Profit, trade.ProfitPercent);

            totalProfit += trade.Profit;
            completedTrades++;
        }

        // Send a single summary message to chat (safe from spam filter)
        var fs = engine.FinancialState;
        await ctx.SendChatAsync(
            $"[Bazaar] Sim done: {completedTrades} trades, P&L {totalProfit:N0}, balance {fs.CoinBalance:N0} (see logs + dashboard)");
    }
}
