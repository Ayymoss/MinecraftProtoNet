using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Bazaar.Trading;

namespace MinecraftProtoNet.ClaudeHarness;

public sealed record PortfolioOptions(
    string Server,
    int Port,
    int HubNumber,
    int MinHubPlayers,
    /// <summary>Coins the bot may have committed to open positions at any one time.</summary>
    double Capital,
    /// <summary>How many products it will hold at once. Legs spend most of their life waiting, so one at a
    /// time wastes the wait.</summary>
    int MaxPositions,
    /// <summary>Stop opening NEW positions after this long. Existing ones are still worked to a close.</summary>
    int TradingMinutes,
    /// <summary>Give up on winding down after this, and report whatever is still open.</summary>
    int WindDownMinutes,
    int PollSeconds,
    double MaxUnitPrice,
    int MaxFillMinutes);

/// <summary>Which half of the flip a position is in.</summary>
public enum PositionSide
{
    Buying,
    Selling,
    Closed
}

/// <summary>
/// One product being flipped, and everything the repricing policy needs to reason about it.
///
/// Quantities and coins come only from Hypixel's own claim messages, never from what we intended to trade, so
/// partial fills and re-prices are accounted for without interpretation.
/// </summary>
public sealed class Position(string productKey, string name, int quantity, double entryMarginPerUnit)
{
    public string ProductKey { get; } = productKey;
    public string Name { get; } = name;
    public int Quantity { get; } = quantity;
    public double EntryMarginPerUnit { get; } = entryMarginPerUnit;

    public PositionSide Side { get; set; } = PositionSide.Buying;
    public double OrderPrice { get; set; }
    public double LegEntryPrice { get; set; }
    public DateTime LegStarted { get; set; } = DateTime.UtcNow;
    public int Steps { get; set; }
    public int PollsBeaten { get; set; }

    /// <summary>Set while an order is live on the Bazaar; cleared once it is claimed or cancelled.</summary>
    public bool OrderLive { get; set; }

    /// <summary>Consecutive polls the order row was missing — one absence is a mid-refresh read, not a fact.</summary>
    public int MissingReads { get; set; }

    public int UnitsBought { get; set; }
    public double CoinsSpent { get; set; }
    public int UnitsSold { get; set; }
    public double CoinsReceived { get; set; }

    public double CostPerUnit => UnitsBought > 0 ? CoinsSpent / UnitsBought : 0;
    public double Profit => CoinsReceived - CoinsSpent;
    public string OrderName => Side == PositionSide.Buying ? $"BUY {Name}" : $"SELL {Name}";

    /// <summary>Coins tied up: escrowed on an open buy, or the cost of goods held on the sell side.</summary>
    public double Committed => Side switch
    {
        PositionSide.Buying => Quantity * OrderPrice,
        PositionSide.Selling => CoinsSpent,
        _ => 0
    };
}

/// <summary>
/// Runs several flips at once for a fixed session, with no human in the loop.
///
/// The shape follows from one fact about the Bazaar: an order spends nearly all its life waiting, and the
/// order manager lists EVERY order on one screen. So the expensive thing (opening menus) is done once per
/// cycle for the whole book of positions, and the cheap thing (deciding) is done per position from measured
/// state. That is what makes five to ten flips an hour possible when a single-position loop would manage one
/// or two.
/// </summary>
public sealed class BazaarPortfolioTask(BazaarSession session, HttpClient api, Action<string> log, StatusServer? status = null)
{
    private const double TaxRate = 0.01125;
    private static readonly (int X, int Y, int Z) HubSelectorPos = (-5, 69, -22);
    private static readonly (int X, int Y, int Z) BazaarPos = (-36, 72, -28);

    private readonly List<Position> _open = [];

    /// <summary>Logs to the console and to the status page, so the page needs no separate instrumentation.</summary>
    private void Say(string message)
    {
        log(message);
        status?.Note(message);
    }

    private readonly List<Position> _closed = [];
    private PortfolioOptions _options = null!;

    public async Task<bool> RunAsync(PortfolioOptions options)
    {
        _options = options;
        session.Subscribe();

        try
        {
            if (!await JoinAndSettleAsync(options)) return false;

            var tradingUntil = DateTime.UtcNow.AddMinutes(options.TradingMinutes);
            var hardStop = tradingUntil.AddMinutes(options.WindDownMinutes);
            var cycle = 0;

            while (DateTime.UtcNow < hardStop)
            {
                cycle++;
                if (session.Intercepted)
                {
                    Say("intercept detected — stopping the session");
                    break;
                }

                try
                {
                    // One failure must not end an hour-long session: a cycle that throws is logged and
                    // retried next time round, because almost every failure here is a transient GUI race.
                    await RunCycleAsync(cycle, DateTime.UtcNow < tradingUntil);
                }
                catch (Exception ex)
                {
                    Say($"cycle {cycle} failed ({ex.GetType().Name}: {ex.Message}) — continuing");
                    try { await session.CloseAsync(); } catch { /* best-effort */ }
                }

                if (_open.Count == 0 && DateTime.UtcNow >= tradingUntil)
                {
                    Say("all positions closed and the trading window is over");
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
            }

            PublishStatus("finished");
            Report();
            return _closed.Sum(p => p.Profit) > 0;
        }
        finally
        {
            session.Unsubscribe();
            try { await session.DisconnectAsync(); } catch { /* best-effort */ }
            Say("disconnected");
        }
    }

    // ===== Session setup =====

    private async Task<bool> JoinAndSettleAsync(PortfolioOptions options)
    {
        if (!await session.ConnectAndSpawnAsync(options.Server, options.Port))
        {
            Say("CONNECT/SPAWN FAILED");
            return false;
        }

        Say("connected + spawned");
        await session.SelectEmptyHotbarSlotAsync();
        await session.SendCommandAsync("skyblock");
        await Task.Delay(TimeSpan.FromSeconds(9));
        await session.SendCommandAsync("hub");
        await Task.Delay(TimeSpan.FromSeconds(6));
        await session.SelectEmptyHotbarSlotAsync();

        if (!await GoToBusyHubAsync(options)) return false;
        return await WalkToBazaarAsync();
    }

    private async Task<bool> GoToBusyHubAsync(PortfolioOptions options)
    {
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20))) return false;
        if (!await session.WalkToAsync(HubSelectorPos, 120)) return false;
        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector")) return false;

        var current = session.MenuSlots().FirstOrDefault(x =>
            x.Item.Contains("red_terracotta", StringComparison.OrdinalIgnoreCase));
        var here = current is null ? null : OccupancyOf(current);

        if (here is { } occupancy && occupancy.Players >= options.MinHubPlayers)
        {
            if (status is not null)
            {
                status.Hub = current?.Name ?? "current hub";
                status.Server = ServerOf(current!) ?? "";
            }
            Say($"current hub holds {occupancy.Players}/{occupancy.Capacity} — busy enough");
            await session.CloseAsync();
            return true;
        }

        var target = session.MenuSlots()
            .Where(x => x.Name is not null && x.Name.Contains("SkyBlock Hub #", StringComparison.OrdinalIgnoreCase))
            .Where(x => current is null || x.Index != current.Index)
            .Select(x => (Slot: x, Occupancy: OccupancyOf(x)))
            .Where(x => x.Occupancy is { } o && o.Players >= options.MinHubPlayers && (o.Capacity == 0 || o.Players < o.Capacity))
            .OrderByDescending(x => x.Occupancy!.Value.Players)
            .FirstOrDefault();

        if (target.Slot is null)
        {
            Say($"no hub reaches {options.MinHubPlayers} players with room — staying put");
            await session.CloseAsync();
            return true;
        }

        Say($"moving to \"{target.Slot.Name}\" at {target.Occupancy!.Value.Players}/{target.Occupancy!.Value.Capacity} " +
            $"({ServerOf(target.Slot) ?? "server unknown"})");
        if (status is not null)
        {
            status.Hub = target.Slot.Name!;
            status.Server = ServerOf(target.Slot) ?? "";
        }
        session.ExpectRelocation = true;
        await session.ClickAsync(target.Slot.Name!);
        await Task.Delay(TimeSpan.FromSeconds(12));
        session.ExpectRelocation = false;
        await session.SelectEmptyHotbarSlotAsync();
        return true;
    }

    private async Task<bool> WalkToBazaarAsync()
    {
        if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(15)))
        {
            if (!await session.WalkToAsync(BazaarPos, 150)) return false;
            if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(20)))
            {
                Say("Bazaar NPC not found");
                return false;
            }
        }
        else if (!await session.WalkToAsync(BazaarPos, 150))
        {
            return false;
        }

        await session.ApproachNpcAsync();
        return true;
    }

    // ===== The cycle =====

    private async Task RunCycleAsync(int cycle, bool mayOpenNew)
    {
        if (!session.Client.IsConnected)
        {
            Say($"cycle {cycle}: disconnected — reconnecting");
            if (!await JoinAndSettleAsync(_options)) Say("reconnect failed; will try again next cycle");
            return;
        }

        if (!await EnsureUsableHubAsync()) return;

        var orders = await ReadOrdersAsync();
        if (orders is null)
        {
            Say($"cycle {cycle}: could not read the order manager");
            return;
        }

        PublishStatus($"cycle {cycle}");
        Say($"cycle {cycle}: {_open.Count} open position(s), {orders.Count} order row(s), " +
            $"{Committed():N0}/{_options.Capital:N0} coins committed, realised {_closed.Sum(p => p.Profit):N1}");

        foreach (var position in _open.ToList())
        {
            await ServicePositionAsync(position, orders);
        }

        _open.RemoveAll(p => p.Side == PositionSide.Closed);

        if (mayOpenNew) await OpenNewPositionsAsync();
    }

    /// <summary>Reads every order row in one menu open — the whole point of batching the cycle.</summary>
    private async Task<List<MenuSlot>?> ReadOrdersAsync()
    {
        if (!await session.OpenNpcMenuAsync("Bazaar")) return null;
        if (!await session.ClickAsync("Manage Orders")) return null;
        await session.WaitForMenuContentAsync(TimeSpan.FromSeconds(5));

        var rows = session.MenuSlots()
            .Where(s => s.Name is not null &&
                        (s.Name.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.StartsWith("SELL ", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await session.CloseAsync();
        return rows;
    }

    private async Task ServicePositionAsync(Position position, List<MenuSlot> orders)
    {
        var row = orders.FirstOrDefault(r =>
            string.Equals(r.Name, position.OrderName, StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            await HandleMissingOrderAsync(position);
            return;
        }

        position.MissingReads = 0;

        var claimable = row.Lore.Any(l =>
            l.Contains("to claim", StringComparison.OrdinalIgnoreCase));
        var status = row.Lore.FirstOrDefault(l => l.Contains("Filled:", StringComparison.OrdinalIgnoreCase)) ?? "unfilled";

        if (claimable)
        {
            await ClaimAsync(position);
            return;
        }

        await ConsiderRepricingAsync(position, status);
    }

    /// <summary>
    /// An order row can vanish because it settled, or because the menu was read mid-refresh. The second is
    /// common enough that believing the first on one reading once turned a completed flip into a reported
    /// 5,000-coin loss, so a disappearance has to persist AND be consistent with the ledger.
    /// </summary>
    private async Task HandleMissingOrderAsync(Position position)
    {
        if (!position.OrderLive)
        {
            await AdvanceAsync(position);
            return;
        }

        position.MissingReads++;
        if (position.MissingReads < 2)
        {
            Say($"  {position.OrderName}: row missing once — re-reading next cycle before believing it");
            return;
        }

        Say($"  {position.OrderName}: gone for {position.MissingReads} reads; treating the leg as settled");
        position.OrderLive = false;
        await AdvanceAsync(position);
    }

    private async Task ClaimAsync(Position position)
    {
        if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders")) return;
        await session.WaitForMenuContentAsync(TimeSpan.FromSeconds(5));

        var mark = session.ChatCount;
        await session.ClickAsync(position.OrderName);
        await Task.Delay(2000);
        await session.CloseAsync();

        foreach (var line in session.ChatSince(mark))
        {
            RecordLedgerLine(position, line);
        }

        // A claim on a partly filled order collects what has landed and leaves the rest working.
        var complete = position.Side == PositionSide.Buying
            ? position.UnitsBought >= position.Quantity
            : position.UnitsSold >= position.UnitsBought;

        if (complete)
        {
            position.OrderLive = false;
            await AdvanceAsync(position);
        }
        else
        {
            Say($"  {position.OrderName}: partial claim ({position.UnitsBought}/{position.Quantity} bought) — order still working");
        }
    }

    /// <summary>Moves a position to its next leg: buy filled → sell it; sell filled → close and book the P&L.</summary>
    private async Task AdvanceAsync(Position position)
    {
        if (position.Side == PositionSide.Buying)
        {
            if (position.UnitsBought == 0)
            {
                Say($"  {position.Name}: buy leg ended with nothing bought — dropping it");
                position.Side = PositionSide.Closed;
                _closed.Add(position);
                return;
            }

            position.Side = PositionSide.Selling;
            position.LegStarted = DateTime.UtcNow;
            position.Steps = 0;
            position.PollsBeaten = 0;
            await PlaceSellAsync(position);
            return;
        }

        if (position.Side == PositionSide.Selling)
        {
            position.Side = PositionSide.Closed;
            _closed.Add(position);
            Say($"  {position.Name}: CLOSED — bought {position.UnitsBought} for {position.CoinsSpent:N1}, " +
                $"sold {position.UnitsSold} for {position.CoinsReceived:N1}, profit {position.Profit:N1}");
        }
    }

    private async Task ConsiderRepricingAsync(Position position, string status)
    {
        var book = await LiveBookAsync(position.ProductKey);
        if (book is null) return;

        var isBuy = position.Side == PositionSide.Buying;
        var ourSide = isBuy ? book.BidPrice : book.AskPrice;
        var otherSide = isBuy ? book.AskPrice : book.BidPrice;
        var levels = (isBuy ? book.BidBook : book.AskBook) ?? [];

        var competing = levels
            .Where(l => isBuy ? l.UnitPrice > position.OrderPrice + 0.001 : l.UnitPrice < position.OrderPrice - 0.001)
            .OrderBy(l => isBuy ? -l.UnitPrice : l.UnitPrice)
            .FirstOrDefault();

        var beaten = isBuy
            ? ourSide > position.OrderPrice + 0.001
            : ourSide is > 0 && ourSide < position.OrderPrice - 0.001;
        position.PollsBeaten = beaten ? position.PollsBeaten + 1 : 0;

        var decision = RepricePolicy.Decide(new RepriceContext(
            IsBuyLeg: isBuy,
            OurPrice: position.OrderPrice,
            CostPerUnit: position.CostPerUnit,
            BestOnOurSide: ourSide,
            BestOnOtherSide: otherSide,
            CompetingDepth: competing?.Amount ?? 0,
            EntryMarginPerUnit: position.EntryMarginPerUnit,
            EntryPrice: position.LegEntryPrice,
            PollsBeaten: position.PollsBeaten,
            StepsTaken: position.Steps,
            Age: DateTime.UtcNow - position.LegStarted,
            DataAge: TimeSpan.FromSeconds(book.DataAgeSeconds)));

        Say($"  {position.OrderName} @ {position.OrderPrice}: {status}; book {book.BidPrice}/{book.AskPrice} " +
            $"({book.DataAgeSeconds:F0}s) -> {decision.Action}: {decision.Reason}");

        switch (decision.Action)
        {
            case RepriceAction.Step:
                if (!await CancelAsync(position)) return;
                position.Steps++;
                // Deliberately re-posted at a price computed from the IN-GAME book, not from the API snapshot
                // that triggered the step. The API is a screening tool that lags by up to a poll; the product
                // page is the live book, and it is already open at the moment of posting. The API decides
                // WHETHER to move, the game decides WHERE to.
                if (isBuy) await PlaceBuyAsync(position, null);
                else await PlaceSellAsync(position, null);
                break;

            case RepriceAction.Abandon:
                if (!await CancelAsync(position)) return;
                Say($"  {position.Name}: abandoning the buy leg to free {position.Committed:N0} coins");
                await AdvanceAsync(position);
                break;

            case RepriceAction.CrossSpread:
                if (!await CancelAsync(position)) return;
                await SellInstantlyAsync(position);
                await AdvanceAsync(position);
                break;
        }
    }

    // ===== Order placement =====

    private async Task OpenNewPositionsAsync()
    {
        while (_open.Count < _options.MaxPositions)
        {
            var available = _options.Capital - Committed();
            if (available < 1000)
            {
                Say($"  only {available:N0} coins uncommitted — not opening another position");
                return;
            }

            var flip = await ChooseFlipAsync(available);
            if (flip is null) return;

            // Size to the smaller of what the API suggests for this budget and what the budget actually
            // covers — the two can disagree when other positions are already holding coins.
            var quantity = Math.Max(1, Math.Min(
                flip.SuggestedQuantity > 0 ? flip.SuggestedQuantity : (int)(available / Math.Max(1, flip.BestBidPrice)),
                (int)(available / Math.Max(1, flip.BestBidPrice))));

            var position = new Position(flip.ProductKey, flip.Name, quantity, flip.EstimatedProfitPerUnit);
            if (!await PlaceBuyAsync(position, null))
            {
                Say($"  could not open a position in {flip.Name}");
                return;
            }

            _open.Add(position);
            Say($"  OPENED {flip.Name} x{quantity} @ {position.OrderPrice} " +
                $"(expected {flip.EstimatedProfitPerUnit:N1}/unit, fill ~{flip.EstimatedRoundTripMinutes:F0}min)");
        }
    }

    private async Task<FlipCandidate?> ChooseFlipAsync(double budget)
    {
        var inFlight = _open.Select(p => p.ProductKey).ToHashSet();
        var url = $"/api/bot/flips?maxResults=40&minScore=2.0&maxPrice={_options.MaxUnitPrice:F0}" +
                  $"&sort=throughput&maxFillMinutes={_options.MaxFillMinutes}&budget={budget:F0}";

        List<FlipCandidate>? flips;
        try
        {
            flips = await api.GetFromJsonAsync<List<FlipCandidate>>(url);
        }
        catch (Exception ex)
        {
            Say($"  flips request failed: {ex.Message}");
            return null;
        }

        if (flips is null || flips.Count == 0)
        {
            Say("  the API has no tradable flips for this budget right now");
            return null;
        }

        // Never double up on a product already in flight: two orders from the same account at the same price
        // level compete with each other, and a re-price on one would be reacting to the other.
        var candidate = flips.FirstOrDefault(f =>
            !inFlight.Contains(f.ProductKey) &&
            !f.IsManipulated &&
            !f.Unstackable &&
            f.EstimatedProfitPerUnit > 0 &&
            f.BestAskPrice <= _options.MaxUnitPrice);

        if (candidate is null) Say("  no candidate left after excluding what we already hold");
        return candidate;
    }

    private async Task<bool> PlaceBuyAsync(Position position, double? price)
    {
        if (!await OpenProductAsync(position.Name)) return false;
        if (session.BestBid is not { } bid || session.BestAsk is not { } ask) return false;

        var buyPrice = price ?? Math.Round(bid + RepricePolicy.Tick, 1);
        var projected = (Math.Round(ask - RepricePolicy.Tick, 1) * (1 - TaxRate)) - buyPrice;
        if (position.OrderPrice == 0 && projected <= 0)
        {
            Say($"  {position.Name}: spread no longer covers tax at live prices ({projected:N1}/unit) — skipping");
            await session.CloseAsync();
            return false;
        }

        if (!await session.ClickAsync("Create Buy Order")) return false;
        if (!await session.ClickAsync("Custom Amount")) return false;
        if (!await session.SignAsync(position.Quantity.ToString())) return false;
        if (!await session.ClickAsync("Custom Price")) return false;
        if (!await session.SignAsync(buyPrice.ToString("0.#"))) return false;

        var mark = session.ChatCount;
        await session.ClickAsync("Buy Order", waitForChange: false);
        await Task.Delay(2500);
        await session.CloseAsync();

        if (!session.ChatSince(mark).Any(l => l.Contains("Buy Order Setup", StringComparison.OrdinalIgnoreCase)))
        {
            Say($"  {position.Name}: no buy-order confirmation in chat");
            return false;
        }

        position.OrderPrice = buyPrice;
        if (position.LegEntryPrice == 0) position.LegEntryPrice = buyPrice;
        position.OrderLive = true;
        position.PollsBeaten = 0;
        return true;
    }

    private async Task<bool> PlaceSellAsync(Position position, double? price = null)
    {
        if (!await OpenProductAsync(position.Name)) return false;
        if (session.BestAsk is not { } ask) return false;

        var sellPrice = price ?? Math.Round(ask - RepricePolicy.Tick, 1);

        // Never below cost: the floor is the price whose post-tax proceeds still clear what we paid.
        if (position.CostPerUnit > 0)
        {
            var floor = Math.Round(position.CostPerUnit / (1 - TaxRate) * 1.01, 1);
            if (sellPrice < floor)
            {
                Say($"  {position.Name}: ask {ask} is under the {floor} cost floor — offering above the book");
                sellPrice = floor;
            }
        }

        if (!await session.ClickAsync("Create Sell Offer")) return false;
        if (!await session.ClickAsync("Custom Price")) return false;
        if (!await session.SignAsync(sellPrice.ToString("0.#"))) return false;

        var mark = session.ChatCount;
        await session.ClickAsync("Sell Offer", waitForChange: false);
        await Task.Delay(2500);
        await session.CloseAsync();

        if (!session.ChatSince(mark).Any(l => l.Contains("Sell Offer Setup", StringComparison.OrdinalIgnoreCase)))
        {
            Say($"  {position.Name}: no sell-offer confirmation in chat");
            return false;
        }

        position.OrderPrice = sellPrice;
        position.LegEntryPrice = sellPrice;
        position.OrderLive = true;
        position.PollsBeaten = 0;
        Say($"  {position.Name}: offering {position.UnitsBought} @ {sellPrice}");
        return true;
    }

    private async Task<bool> SellInstantlyAsync(Position position)
    {
        if (!await OpenProductAsync(position.Name)) return false;

        var mark = session.ChatCount;
        if (!await session.ClickAsync("Sell Instantly")) return false;
        await Task.Delay(2500);
        await session.CloseAsync();

        foreach (var line in session.ChatSince(mark)) RecordLedgerLine(position, line);
        return true;
    }

    private async Task<bool> CancelAsync(Position position)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (await TryCancelAsync(position)) return true;
            Say($"  {position.OrderName}: cancel attempt {attempt}/3 failed");
            await session.CloseAsync();
            await Task.Delay(1500);
        }
        return false;
    }

    private async Task<bool> TryCancelAsync(Position position)
    {
        if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders")) return false;
        await session.WaitForMenuContentAsync(TimeSpan.FromSeconds(5));
        if (!await session.ClickAsync(position.OrderName, button: 1)) return false;

        if (!session.ContainerTitle.Contains("Order options", StringComparison.OrdinalIgnoreCase))
        {
            await session.CloseAsync();
            return false;
        }

        var mark = session.ChatCount;
        await session.ClickAsync("Cancel Order");
        await Task.Delay(2500);
        await session.CloseAsync();

        // A cancel refunds coins on a buy and returns goods on a sell; both are ledger events.
        foreach (var line in session.ChatSince(mark)) RecordLedgerLine(position, line);

        var cancelled = session.ChatSince(mark).Any(l => l.Contains("Cancelling order", StringComparison.OrdinalIgnoreCase)
                                                         || l.Contains("Cancelled!", StringComparison.OrdinalIgnoreCase));
        if (cancelled) position.OrderLive = false;
        return cancelled;
    }

    private async Task<bool> OpenProductAsync(string productName)
    {
        if (!await EnsureUsableHubAsync()) return false;
        if (!await session.OpenNpcMenuAsync("Bazaar")) return false;
        if (!await session.ClickAsync("Search")) return false;
        if (!await session.SignAsync(productName)) return false;
        if (!await session.ClickAsync(productName)) return false;

        if (!session.ContainerTitle.Contains(productName, StringComparison.OrdinalIgnoreCase))
        {
            Say($"  expected the {productName} page, got \"{session.ContainerTitle}\"");
            return false;
        }
        return true;
    }

    // ===== Ledger =====

    private static readonly System.Text.RegularExpressions.Regex ClaimedItems =
        new(@"Claimed\s+([\d,]+)x\s+.*?bought for\s+([\d,]+(?:\.\d+)?)\s+each",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex ClaimedCoins =
        new(@"Claimed\s+([\d,]+(?:\.\d+)?)\s+coins from selling\s+([\d,]+)x",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex SoldInstantly =
        new(@"Sold\s+([\d,]+)x\s+.*?for\s+([\d,]+(?:\.\d+)?)\s+coins",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex Refunded =
        new(@"Refunded\s+([\d,]+(?:\.\d+)?)\s+coins",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Every number in the P&L comes from a message Hypixel sent. Nothing is inferred from what we asked for,
    /// which is what makes partial fills, re-prices and cancellations add up on their own.
    /// </summary>
    private void RecordLedgerLine(Position position, string line)
    {
        var items = ClaimedItems.Match(line);
        if (items.Success)
        {
            var qty = (int)Num(items.Groups[1].Value);
            var unit = Num(items.Groups[2].Value);
            position.UnitsBought += qty;
            position.CoinsSpent += qty * unit;
            Say($"  ledger {position.Name}: +{qty} @ {unit} (held {position.UnitsBought}, spent {position.CoinsSpent:N1})");
            return;
        }

        var coins = ClaimedCoins.Match(line);
        if (coins.Success)
        {
            position.CoinsReceived += Num(coins.Groups[1].Value);
            position.UnitsSold += (int)Num(coins.Groups[2].Value);
            Say($"  ledger {position.Name}: sold {position.UnitsSold} for {position.CoinsReceived:N1}");
            return;
        }

        var sold = SoldInstantly.Match(line);
        if (sold.Success)
        {
            position.UnitsSold += (int)Num(sold.Groups[1].Value);
            position.CoinsReceived += Num(sold.Groups[2].Value);
            Say($"  ledger {position.Name}: instant-sold {position.UnitsSold} for {position.CoinsReceived:N1}");
            return;
        }

        // A refund on a cancelled BUY returns coins that were never spent, so it reduces cost rather than
        // counting as income. Goods are refunded on a cancelled sell and cost nothing to take back.
        var refund = Refunded.Match(line);
        if (refund.Success && position.Side == PositionSide.Buying)
        {
            Say($"  ledger {position.Name}: {Num(refund.Groups[1].Value):N1} coins refunded from the cancelled order");
        }
    }

    private static double Num(string text) =>
        double.TryParse(text.Replace(",", ""), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    private double Committed() => _open.Sum(p => p.Committed);

    /// <summary>Copies the trader's state onto the status page. Pure reporting — it decides nothing.</summary>
    private void PublishStatus(string state)
    {
        if (status is null) return;

        var self = session.Client.State.LocalPlayer?.Entity?.Position;
        status.State = state;
        status.Connected = session.Client.IsConnected;
        status.Intercepted = session.Intercepted;
        status.Position = self is null ? "" : $"({self.X:F0}, {self.Y:F0}, {self.Z:F0})";
        status.Capital = _options.Capital;
        status.Committed = Committed();
        status.RealisedProfit = _closed.Sum(p => p.Profit);
        status.ClosedCount = _closed.Count;
        status.ProfitableCount = _closed.Count(p => p.Profit > 0);
        status.OpenPositions = _open.Select(object (p) => new
        {
            name = p.Name,
            side = p.Side.ToString(),
            quantity = p.Quantity,
            price = p.OrderPrice,
            unitsBought = p.UnitsBought,
            spent = p.CoinsSpent,
            ageMinutes = (DateTime.UtcNow - p.LegStarted).TotalMinutes,
            steps = p.Steps
        }).ToList();
        status.ClosedPositions = _closed.Select(object (p) => new
        {
            name = p.Name,
            unitsBought = p.UnitsBought,
            spent = p.CoinsSpent,
            unitsSold = p.UnitsSold,
            received = p.CoinsReceived,
            profit = p.Profit
        }).ToList();
    }

    private void Report()
    {
        Say("=========================================================");
        Say($"SESSION OVER — {_closed.Count} position(s) closed, {_open.Count} still open");
        foreach (var p in _closed)
        {
            Say($"  {p.Name,-30} bought {p.UnitsBought,4} for {p.CoinsSpent,10:N1}  " +
                $"sold {p.UnitsSold,4} for {p.CoinsReceived,10:N1}  = {p.Profit,10:N1}");
        }
        foreach (var p in _open)
        {
            Say($"  {p.Name,-30} STILL OPEN on the {p.Side} leg @ {p.OrderPrice} " +
                $"(bought {p.UnitsBought}, spent {p.CoinsSpent:N1})");
        }

        var realised = _closed.Sum(p => p.Profit);
        var winners = _closed.Count(p => p.Profit > 0);
        Say($"  realised P&L: {realised:N1} coins across {_closed.Count} closed ({winners} profitable)");
        Say("=========================================================");
    }

    // ===== Shared plumbing =====

    private async Task<bool> EnsureUsableHubAsync()
    {
        if (session.Intercepted) return false;

        if (session.Evacuated)
        {
            session.StopMoving();
            Say("evacuated to the island — standing still, then warping out");
            await Task.Delay(TimeSpan.FromSeconds(10));
            await session.SendCommandAsync("hub");
            await Task.Delay(TimeSpan.FromSeconds(12));
            session.ClearRestartState();
            await session.SelectEmptyHotbarSlotAsync();
            return await GoToBusyHubAsync(_options) && await WalkToBazaarAsync();
        }

        if (session.RestartWarningAt is not null || session.OutageNotice is not null)
        {
            Say($"leaving this hub ({session.OutageNotice ?? "restart warning"})");
            session.ClearRestartState();
            session.ClearOutage();
            return await GoToBusyHubAsync(_options) && await WalkToBazaarAsync();
        }

        return true;
    }

    private async Task<LiveProduct?> LiveBookAsync(string productKey)
    {
        try
        {
            return await api.GetFromJsonAsync<LiveProduct>($"/api/bot/products/{productKey}");
        }
        catch (Exception ex)
        {
            Say($"  live book lookup failed ({ex.Message})");
            return null;
        }
    }

    private static (int Players, int Capacity)? OccupancyOf(MenuSlot slot)
    {
        foreach (var line in slot.Lore)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line, @"Players?\s*:?\s*([\d,]+)\s*(?:/\s*([\d,]+))?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups[1].Value.Replace(",", ""), out var players)) continue;
            var capacity = match.Groups[2].Success && int.TryParse(match.Groups[2].Value.Replace(",", ""), out var cap) ? cap : 0;
            return (players, capacity);
        }
        return null;
    }

    private static string? ServerOf(MenuSlot slot) =>
        slot.Lore.FirstOrDefault(l => l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))?.Trim();

    private sealed record FlipCandidate(
        [property: JsonPropertyName("productKey")] string ProductKey,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("bestBidPrice")] double BestBidPrice,
        [property: JsonPropertyName("bestAskPrice")] double BestAskPrice,
        [property: JsonPropertyName("estimatedProfitPerUnit")] double EstimatedProfitPerUnit,
        [property: JsonPropertyName("estimatedRoundTripMinutes")] double EstimatedRoundTripMinutes,
        [property: JsonPropertyName("suggestedQuantity")] int SuggestedQuantity,
        [property: JsonPropertyName("isManipulated")] bool IsManipulated,
        [property: JsonPropertyName("unstackable")] bool Unstackable);

    private sealed record LiveProduct(
        [property: JsonPropertyName("bidPrice")] double BidPrice,
        [property: JsonPropertyName("askPrice")] double AskPrice,
        [property: JsonPropertyName("dataAgeSeconds")] double DataAgeSeconds,
        [property: JsonPropertyName("bidBook")] List<BookLevel>? BidBook,
        [property: JsonPropertyName("askBook")] List<BookLevel>? AskBook);

    private sealed record BookLevel(
        [property: JsonPropertyName("unitPrice")] double UnitPrice,
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("orders")] int Orders);
}
