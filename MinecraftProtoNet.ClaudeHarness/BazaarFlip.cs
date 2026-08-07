using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>One flip candidate as BazaarCompanion scores it. Mirrors the API's FlipOpportunity record.</summary>
public sealed record FlipOpportunity(
    [property: JsonPropertyName("productKey")] string ProductKey,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("bestBidPrice")] double BestBidPrice,
    [property: JsonPropertyName("bestAskPrice")] double BestAskPrice,
    [property: JsonPropertyName("bidWeekVolume")] double BidWeekVolume,
    [property: JsonPropertyName("askWeekVolume")] double AskWeekVolume,
    [property: JsonPropertyName("spreadPercent")] double SpreadPercent,
    [property: JsonPropertyName("estimatedProfitPerUnit")] double EstimatedProfitPerUnit,
    [property: JsonPropertyName("opportunityScore")] double OpportunityScore,
    [property: JsonPropertyName("isManipulated")] bool IsManipulated,
    [property: JsonPropertyName("unstackable")] bool Unstackable,
    // Present only on API builds that expose queue depth; 0 on older ones, which the caller treats as
    // "unknown" and falls back to its own ranking.
    [property: JsonPropertyName("estimatedRoundTripMinutes")] double EstimatedRoundTripMinutes = 0,
    [property: JsonPropertyName("topBidDepth")] int TopBidDepth = 0,
    [property: JsonPropertyName("topAskDepth")] int TopAskDepth = 0);

public sealed record FlipOptions(
    string Server,
    int Port,
    int HubNumber,
    int Quantity,
    double MaxUnitPrice,
    string? ForceProduct,
    int MonitorMinutes,
    int PollSeconds,
    /// <summary>How many times a leg may be cancelled and re-posted after being undercut.</summary>
    int MaxReprices = 5,
    /// <summary>Seconds an order is left alone after being outbid, in case it fills anyway.</summary>
    int RepricePatienceSeconds = 45,
    /// <summary>Ceiling on the API's estimated round-trip fill time. Ignored by API builds without it.</summary>
    int MaxFillMinutes = 90,
    /// <summary>
    /// Fewest players a hub may hold before the bot will settle in it. A busy hub is cover: a lone player
    /// standing at the Bazaar for an hour is conspicuous in a way that one of forty is not. 0 disables the
    /// check and takes whatever hub was asked for.
    /// </summary>
    int MinHubPlayers = 20);

/// <summary>
/// End-to-end Bazaar flip driven by BazaarCompanion: ask the API what is worth flipping, place a buy order at
/// the top of the book, wait for it to fill, claim the goods, offer them back at the top of the sell side, wait
/// for that to fill, and claim the coins.
///
/// The waits are the point — orders priced inside the spread do not fill instantly, so the bot stays connected
/// and re-opens the order manager on a timer rather than the run being a one-shot chain.
///
/// Money is real. Every coin-spending click is deliberate and logged with the price it was quoted at.
/// </summary>
public sealed class BazaarFlipTask(BazaarSession session, HttpClient api, Action<string> log)
{
    /// <summary>Hypixel's Bazaar tax on sell proceeds — matches BazaarCompanion's own constant.</summary>
    private const double TaxRate = 0.01125;

    private static readonly (int X, int Y, int Z) HubSelectorPos = (-5, 69, -22);
    private static readonly (int X, int Y, int Z) BazaarPos = (-36, 72, -28);

    public async Task<bool> RunAsync(FlipOptions options)
    {
        _options = options;
        session.Subscribe();
        try
        {
            if (!await session.ConnectAndSpawnAsync(options.Server, options.Port))
            {
                log("CONNECT/SPAWN FAILED");
                return false;
            }
            log("connected + spawned");
            await session.SelectEmptyHotbarSlotAsync();

            await session.SendCommandAsync("skyblock");
            await Task.Delay(TimeSpan.FromSeconds(9));
            await session.SendCommandAsync("hub");
            await Task.Delay(TimeSpan.FromSeconds(6));

            if (!await GoToHubAsync(options.HubNumber, options.MinHubPlayers)) return false;

            // Pick the product BEFORE walking: if the API has nothing tradable there is no point being here.
            var flip = await ChooseFlipAsync(options);
            if (flip is null) return false;

            if (!await WalkToBazaarAsync()) return false;

            var placed = await AcquireAsync(flip, options);
            if (placed is null) return false;

            var bought = placed.Value.Filled;
            if (!bought)
            {
                log($"buy order for {flip.Name} did not fill within {options.MonitorMinutes} minutes — " +
                    "it is still live on the Bazaar and can be claimed or cancelled later");
                return false;
            }

            var offered = await LiquidateAsync(flip, options);
            if (offered is null) return false;

            var sold = offered.Value.Filled;
            if (!sold)
            {
                log($"sell offer for {flip.Name} did not fill within {options.MonitorMinutes} minutes — " +
                    "it is still live and holds the goods");
                return false;
            }

            return ReportLedger(flip);
        }
        finally
        {
            session.Unsubscribe();
            try { await session.DisconnectAsync(); } catch { /* best-effort */ }
            log("disconnected");
        }
    }

    /// <summary>Final P&L, straight off the ledger of claim messages.</summary>
    private bool ReportLedger(FlipOpportunity flip)
    {
        var profit = _coinsReceived - _coinsSpent;
        log("=========================================================");
        log($"FLIP COMPLETE — {flip.Name}");
        log($"  bought {_unitsBought}x for {_coinsSpent:F1} coins");
        log($"  sold   {_unitsSold}x for {_coinsReceived:F1} coins (net of {TaxRate:P2} tax)");
        if (_unitsBought != _unitsSold)
            log($"  NOTE: {_unitsBought - _unitsSold} unit(s) still held — profit below counts only what was sold");
        log($"  PROFIT = {profit:F1} coins");
        log("=========================================================");
        return profit > 0;
    }

    // ===== Hub =====

    /// <summary>
    /// Walks to the Hub Selector and switches to the requested hub, so a human can follow the bot. The hub the
    /// account is already in is marked with red terracotta rather than quartz; clicking it is pointless, so
    /// that case just carries on.
    /// </summary>
    private async Task<bool> GoToHubAsync(int hubNumber, int minPlayers)
    {
        log($"--- going to Hub #{hubNumber} ---");
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20)))
        {
            log("Hub Selector not found");
            return false;
        }

        if (!await session.WalkToAsync(HubSelectorPos, 90)) return false;
        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector"))
        {
            log("hub selector menu did not open");
            session.LogMenu();
            return false;
        }

        var wanted = $"SkyBlock Hub #{hubNumber}";
        var slot = session.FindSlot(wanted);
        if (slot is null)
        {
            log($"\"{wanted}\" is not in \"{session.ContainerTitle}\"");
            session.LogMenu();
            return false;
        }

        // Prefer a busy hub when the requested one is quiet — see MinHubPlayers.
        if (minPlayers > 0)
        {
            var requested = OccupancyOf(slot);
            if (requested is null)
            {
                log("hub entries carry no player count in their lore — keeping the requested hub");
            }
            else if (requested.Value.Players < minPlayers)
            {
                // Busiest hub that still has room. Full hubs are excluded because connecting to one just
                // bounces straight back out.
                var busiest = session.MenuSlots()
                    .Where(x => x.Name is not null && x.Name.Contains("SkyBlock Hub #", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (Slot: x, Occupancy: OccupancyOf(x)))
                    .Where(x => x.Occupancy is { } o && o.Players >= minPlayers && (o.Capacity == 0 || o.Players < o.Capacity))
                    .OrderByDescending(x => x.Occupancy!.Value.Players)
                    .FirstOrDefault();

                if (busiest.Slot is null)
                {
                    log($"Hub #{hubNumber} holds {requested.Value.Players} and no hub reaches {minPlayers} with room — staying put");
                }
                else
                {
                    log($"Hub #{hubNumber} holds only {requested.Value.Players}/{requested.Value.Capacity}; " +
                        $"moving to \"{busiest.Slot.Name}\" at {busiest.Occupancy!.Value.Players}/" +
                        $"{busiest.Occupancy!.Value.Capacity} for cover ({ServerOf(busiest.Slot) ?? "server unknown"})");
                    wanted = busiest.Slot.Name!;
                    slot = busiest.Slot;
                }
            }
            else
            {
                log($"Hub #{hubNumber} holds {requested.Value.Players}/{requested.Value.Capacity} — busy enough " +
                    $"({ServerOf(slot) ?? "server unknown"})");
            }
        }

        if (slot.Item.Contains("red_terracotta", StringComparison.OrdinalIgnoreCase))
        {
            log($"already in Hub #{hubNumber} — no switch needed");
            await session.CloseAsync();
            return true;
        }

        var chatMark = session.ChatCount;
        session.ExpectRelocation = true;
        await session.ClickAsync(wanted);

        // The switch is a full server change: Start Configuration, a new Login, then a fresh spawn.
        log("waiting for the hub switch to land");
        await Task.Delay(TimeSpan.FromSeconds(12));
        session.ExpectRelocation = false;
        await session.SelectEmptyHotbarSlotAsync();
        foreach (var line in session.ChatSince(chatMark).Where(l => l.Contains("Hub", StringComparison.OrdinalIgnoreCase)))
        {
            log($"  {line}");
        }
        return true;
    }

    /// <summary>
    /// Occupancy of a hub, read off the entry's lore — Hypixel writes "Players: 48/60". Capacity comes back
    /// too so a hub that is already full can be skipped: clicking one just bounces you. Null means the lore
    /// had no count, which is deliberately distinct from an empty hub.
    /// </summary>
    private static (int Players, int Capacity)? OccupancyOf(MenuSlot slot)
    {
        foreach (var line in slot.Lore)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line, @"Players?\s*:?\s*([\d,]+)\s*(?:/\s*([\d,]+))?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups[1].Value.Replace(",", ""), out var players)) continue;
            var capacity = match.Groups[2].Success && int.TryParse(match.Groups[2].Value.Replace(",", ""), out var cap)
                ? cap
                : 0;
            return (players, capacity);
        }
        return null;
    }

    /// <summary>The backend name Hypixel prints in the hub entry ("Server: mega9E") — handy for following the bot.</summary>
    private static string? ServerOf(MenuSlot slot) =>
        slot.Lore.FirstOrDefault(l => l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))?.Trim();

    /// <summary>
    /// Moves to a different hub and returns to its Bazaar. SkyBlock turns the Bazaar off on a hub that is
    /// struggling, and the outage is per-server, so the fix is to be on another server — the same reason a
    /// human would hop. The hub currently occupied is excluded so the hop cannot pick the broken one again.
    /// </summary>
    private async Task<bool> RecoverFromOutageAsync(FlipOptions options)
    {
        log($"--- Bazaar unavailable here: {session.OutageNotice} ---");
        session.ClearOutage();
        return await HopToBusyHubAsync(options, forceHop: true);
    }

    /// <summary>
    /// Ensures the bot ends up at a Bazaar on a hub with enough people in it.
    ///
    /// <paramref name="forceHop"/> leaves the current hub even if it is busy — used when THIS server is the
    /// problem (Bazaar disabled, or about to reboot). Otherwise the hop only happens when the hub is too
    /// quiet, which is the usual state after /hub drops the bot into whichever hub it likes.
    /// </summary>
    private async Task<bool> HopToBusyHubAsync(FlipOptions options, bool forceHop)
    {
        await session.CloseAsync();
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20)))
        {
            // The selector lives near spawn, not at the Bazaar, so walk back before looking again.
            if (!await session.WalkToAsync(HubSelectorPos, 120)) return false;
            if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20))) return false;
        }
        else if (!await session.WalkToAsync(HubSelectorPos, 120))
        {
            return false;
        }

        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector")) return false;

        var current = session.MenuSlots().FirstOrDefault(x =>
            x.Item.Contains("red_terracotta", StringComparison.OrdinalIgnoreCase));

        if (!forceHop && current is not null && OccupancyOf(current) is { } here && here.Players >= options.MinHubPlayers)
        {
            log($"current hub already holds {here.Players}/{here.Capacity} — staying");
            await session.CloseAsync();
            return await WalkToBazaarAsync();
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
            log($"no other hub has {options.MinHubPlayers}+ players with room — cannot hop");
            return false;
        }

        log($"hopping to \"{target.Slot.Name}\" at {target.Occupancy!.Value.Players}/{target.Occupancy!.Value.Capacity} " +
            $"({ServerOf(target.Slot) ?? "server unknown"})");
        session.ExpectRelocation = true;
        await session.ClickAsync(target.Slot.Name!);
        await Task.Delay(TimeSpan.FromSeconds(12));
        session.ExpectRelocation = false;
        await session.SelectEmptyHotbarSlotAsync();

        session.ClearOutage();
        return await WalkToBazaarAsync();
    }

    /// <summary>
    /// Handles the two ways a hub stops being usable. Called before every trading action and on every poll.
    ///
    /// A reboot announces itself ("This server will restart soon" / "60 seconds to warp out"), then evacuates
    /// everyone to their private island and prints "Evacuating to Your Island...". The island is the dangerous
    /// case: the bot arrives somewhere it did not choose, above a void, so it must not move a muscle — it
    /// stands still, waits, and uses /hub to leave. The wait is not politeness: SkyBlock rejects a warp issued
    /// too soon after the forced one ("too many teleports").
    ///
    /// The warning is the better outcome to act on, because leaving before the evacuation skips the island
    /// entirely.
    /// </summary>
    private async Task<bool> EnsureUsableHubAsync(FlipOptions options)
    {
        // Checked here because every trading action and every poll passes through this method, so there is no
        // path that keeps trading after the latch trips.
        if (session.Intercepted)
        {
            log("halting: an intercept was detected");
            return false;
        }

        if (session.Evacuated)
        {
            session.StopMoving();
            log("evacuated to the island — standing still for 10s before warping out");
            await Task.Delay(TimeSpan.FromSeconds(10));

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                await session.SendCommandAsync("hub");
                await Task.Delay(TimeSpan.FromSeconds(10));

                await session.SelectEmptyHotbarSlotAsync();
                if (await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(10))
                    || await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(10)))
                {
                    session.ClearRestartState();
                    log("back in a hub after the reboot — checking how busy it is");
                    // /hub picks the hub, and it is usually a quiet one, so the population rule has to run
                    // again here rather than only at startup.
                    return await HopToBusyHubAsync(options, forceHop: false);
                }

                log($"/hub attempt {attempt}/3 did not land us in a hub; waiting before retrying");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            log("could not get off the island");
            return false;
        }

        if (session.RestartWarningAt is not null)
        {
            // Still on a doomed hub, but not yet evacuated: hop now and skip the island entirely.
            log("restart warning on this hub — moving to another one before the evacuation");
            session.ClearRestartState();
            return await RecoverFromOutageAsync(options);
        }

        if (session.OutageNotice is not null)
        {
            return await RecoverFromOutageAsync(options);
        }

        return true;
    }

    private async Task<bool> WalkToBazaarAsync()
    {
        log("--- walking to the Bazaar ---");
        if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(20)))
        {
            // Entities only stream in near the destination, so a miss at spawn is expected — walk first.
            if (!await session.WalkToAsync(BazaarPos, 120)) return false;
            if (!await session.FindNpcAsync("Bazaar", TimeSpan.FromSeconds(20)))
            {
                log("Bazaar NPC not found");
                return false;
            }
        }
        else if (!await session.WalkToAsync(BazaarPos, 120))
        {
            return false;
        }

        await session.ApproachNpcAsync();
        return true;
    }

    // ===== Choosing what to flip =====

    private async Task<FlipOpportunity?> ChooseFlipAsync(FlipOptions options)
    {
        log("--- asking BazaarCompanion what to flip ---");
        // sort/maxFillMinutes are ignored by an API build that predates them (unbound query params), so this
        // is safe to send either way: today it returns the score-ordered list and the local ranking below
        // decides; once BazaarCompanion ships the fill-time fields, the server does the ranking properly.
        var url = $"/api/bot/flips?maxResults=40&minScore=2.5&maxPrice={options.MaxUnitPrice:F0}" +
                  $"&sort=throughput&maxFillMinutes={options.MaxFillMinutes}";
        List<FlipOpportunity>? flips;
        try
        {
            flips = await api.GetFromJsonAsync<List<FlipOpportunity>>(url);
        }
        catch (Exception ex)
        {
            log($"flips request failed: {ex.Message}");
            return null;
        }

        if (flips is null || flips.Count == 0)
        {
            log("the API returned no flip opportunities");
            return null;
        }

        // Liquidity first, not headline margin: a 600% spread on a product that trades twice a day never
        // fills, and an unfilled order is not a profit. Both sides have to be busy because both sides have to
        // fill — the buy AND the sell.
        var tradable = flips
            .Where(f => !f.IsManipulated && !f.Unstackable)
            .Where(f => f.BestAskPrice <= options.MaxUnitPrice)
            .Where(f => f.EstimatedProfitPerUnit > 0)
            .ToList();

        // If the API told us how long each flip takes to complete, it has already ordered them by profit per
        // minute and there is nothing to second-guess. Otherwise fall back to two-sided weekly volume.
        var serverRanked = tradable.Any(f => f.EstimatedRoundTripMinutes > 0);
        var ranked = serverRanked
            ? tradable
            : tradable.OrderByDescending(f => Math.Min(f.BidWeekVolume, f.AskWeekVolume)).ToList();
        log(serverRanked
            ? "using the API's throughput ranking (fill-time fields present)"
            : "API has no fill-time fields yet — ranking locally by two-sided weekly volume");

        log($"{flips.Count} candidates, {ranked.Count} tradable; top by two-sided liquidity:");
        foreach (var f in ranked.Take(5))
        {
            log($"  {f.Name,-28} bid {f.BestBidPrice,9:F1} ask {f.BestAskPrice,9:F1} " +
                $"spread {f.SpreadPercent,6:F1}% profit/u {f.EstimatedProfitPerUnit,8:F1} " +
                $"vol/wk {Math.Min(f.BidWeekVolume, f.AskWeekVolume),12:N0}" +
                (f.EstimatedRoundTripMinutes > 0
                    ? $" fill~{f.EstimatedRoundTripMinutes,6:F0}min (queue {f.TopBidDepth:N0}/{f.TopAskDepth:N0})"
                    : ""));
        }

        var chosen = options.ForceProduct is { Length: > 0 } forced
            ? ranked.FirstOrDefault(f => f.Name.Contains(forced, StringComparison.OrdinalIgnoreCase))
              ?? flips.FirstOrDefault(f => f.Name.Contains(forced, StringComparison.OrdinalIgnoreCase))
            : ranked.FirstOrDefault();

        if (chosen is null)
        {
            log("no candidate passed the filters");
            return null;
        }

        _productKey = chosen.ProductKey;
        log($"chosen: {chosen.Name} [{chosen.ProductKey}] (score {chosen.OpportunityScore:F1})");
        return chosen;
    }

    // ===== Trading =====

    /// <summary>Opens the product page for a product by name, via the Bazaar's own search.</summary>
    private async Task<bool> OpenProductAsync(string productName)
    {
        if (!await EnsureUsableHubAsync(_options)) return false;

        if (!await session.OpenNpcMenuAsync("Bazaar"))
        {
            // A hub whose Bazaar is disabled answers a right-click with a chat line instead of a menu, so a
            // failure to open is where the outage surfaces first.
            log("Bazaar menu did not open");
            session.LogMenu();
            return false;
        }

        if (!await session.ClickAsync("Search")) return false;
        if (!await session.SignAsync(productName)) return false;
        if (!await session.ClickAsync(productName)) return false;

        if (!session.ContainerTitle.Contains(productName, StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the {productName} product page, got \"{session.ContainerTitle}\"");
            return false;
        }
        return true;
    }

    private async Task<(double UnitPrice, double Cost)?> PlaceBuyOrderAsync(FlipOpportunity flip, int quantity)
    {
        log($"--- placing a buy order: {quantity}x {flip.Name} ---");
        if (!await OpenProductAsync(flip.Name)) return null;

        if (session.BestBid is not { } bid || session.BestAsk is not { } ask)
        {
            log("could not read the order book off the product page");
            return null;
        }

        // Sit one tick above the best buy order so we are first in the queue, and confirm the flip still pays
        // after tax at the prices the game is quoting right now rather than the ones the API cached.
        var buyPrice = Math.Round(bid + 0.1, 1);
        var sellPrice = Math.Round(ask - 0.1, 1);
        var margin = sellPrice * (1 - TaxRate) - buyPrice;
        log($"live book: bid {bid} / ask {ask} -> buy at {buyPrice}, plan to sell at {sellPrice}, " +
            $"margin {margin:F1}/unit ({margin * quantity:F1} total)");

        if (margin <= 0)
        {
            log("the spread does not cover the tax at live prices — not trading");
            return null;
        }

        if (!await session.ClickAsync("Create Buy Order")) return null;
        if (!await session.ClickAsync("Custom Amount")) return null;
        if (!await session.SignAsync(quantity.ToString())) return null;
        if (!await session.ClickAsync("Custom Price")) return null;
        if (!await session.SignAsync(buyPrice.ToString("0.#"))) return null;

        var confirm = session.FindSlot("Buy Order");
        if (confirm is null || !session.ContainerTitle.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the buy-order confirmation, got \"{session.ContainerTitle}\"");
            return null;
        }
        log($"confirming: {string.Join(" / ", confirm.Lore.Where(l => l.Trim().Length > 0))}");

        var chatMark = session.ChatCount;
        await session.ClickAsync("Buy Order", waitForChange: false);
        await Task.Delay(3000);

        var setup = session.ChatSince(chatMark).FirstOrDefault(l => l.Contains("Buy Order Setup", StringComparison.OrdinalIgnoreCase));
        if (setup is null)
        {
            log("no confirmation in chat — assuming the order was NOT placed");
            foreach (var line in session.ChatSince(chatMark)) log($"  chat: {line}");
            return null;
        }

        log($"ORDER PLACED: {setup}");
        await session.CloseAsync();
        return (buyPrice, buyPrice * quantity);
    }

    private async Task<(double UnitPrice, double Proceeds)?> PlaceSellOfferAsync(FlipOpportunity flip)
    {
        log($"--- placing a sell offer for {flip.Name} ---");
        if (!await OpenProductAsync(flip.Name)) return null;

        if (session.BestAsk is not { } ask)
        {
            log("could not read the ask off the product page");
            return null;
        }

        var sellPrice = Math.Round(ask - 0.1, 1);

        // Never sell below what the goods cost. Chasing a falling ask is how a market-making loop turns a
        // spread into a loss: the ask dropped 33 coins in one poll during the first live run, and without a
        // floor the next re-price would have followed it down indefinitely. The floor is the price at which
        // post-tax proceeds still clear the recorded cost basis, so an order can sit above the book and wait
        // rather than crystallise a loss.
        if (_unitsBought > 0)
        {
            var costPerUnit = _coinsSpent / _unitsBought;
            var breakEven = costPerUnit / (1 - TaxRate);
            var floor = Math.Round(breakEven * 1.01, 1);
            if (sellPrice < floor)
            {
                log($"market ask {ask} is below the {floor} floor (cost {costPerUnit:F1}/unit + {TaxRate:P2} tax) " +
                    "— holding above the book instead of selling at a loss");
                sellPrice = floor;
            }
        }

        log($"live ask {ask} -> offering at {sellPrice}");

        // No amount screen on this side: a sell offer covers everything of that product you are holding.
        if (!await session.ClickAsync("Create Sell Offer")) return null;
        if (!await session.ClickAsync("Custom Price")) return null;
        if (!await session.SignAsync(sellPrice.ToString("0.#"))) return null;

        var confirm = session.FindSlot("Sell Offer");
        if (confirm is null || !session.ContainerTitle.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
        {
            log($"expected the sell-offer confirmation, got \"{session.ContainerTitle}\"");
            return null;
        }
        log($"confirming: {string.Join(" / ", confirm.Lore.Where(l => l.Trim().Length > 0))}");

        // "You earn: N coins" is the post-tax figure, which is the number that decides whether this was profitable.
        var proceeds = ParseCoins(confirm.Lore.FirstOrDefault(l => l.Contains("You earn", StringComparison.OrdinalIgnoreCase)));

        var chatMark = session.ChatCount;
        await session.ClickAsync("Sell Offer", waitForChange: false);
        await Task.Delay(3000);

        var setup = session.ChatSince(chatMark).FirstOrDefault(l => l.Contains("Sell Offer Setup", StringComparison.OrdinalIgnoreCase));
        if (setup is null)
        {
            log("no confirmation in chat — assuming the offer was NOT placed");
            foreach (var line in session.ChatSince(chatMark)) log($"  chat: {line}");
            return null;
        }

        log($"OFFER PLACED: {setup}");
        await session.CloseAsync();
        return (sellPrice, proceeds ?? 0);
    }

    // ===== Legs with repricing =====

    /// <summary>
    /// Buys the goods: post at the top of the buy book, and if somebody undercuts by the customary 0.1 coins,
    /// cancel and re-post above them rather than sitting behind a wall.
    ///
    /// This is the difference between a limit order and a wish. The first attempt at this flip sat at 1258.6
    /// with 11,000 units queued ahead at 1258.8 — an hour of waiting for a fill that a 0.2-coin move would
    /// have got in minutes. Chasing is bounded: the price can never rise past the point where the flip stops
    /// paying for itself after tax.
    /// </summary>
    private async Task<(double UnitPrice, double Cost, bool Filled)?> AcquireAsync(FlipOpportunity flip, FlipOptions options)
    {
        var orderName = $"BUY {flip.Name}";
        for (var attempt = 1; attempt <= options.MaxReprices + 1; attempt++)
        {
            var placed = await PlaceBuyOrderAsync(flip, options.Quantity);
            if (placed is null) return null;

            var outcome = await WatchOrderAsync(orderName, "items to claim", placed.Value.UnitPrice, isBuy: true, options);
            switch (outcome)
            {
                case OrderOutcome.Claimed:
                    return (placed.Value.UnitPrice, placed.Value.Cost, true);

                case OrderOutcome.Outbid when attempt <= options.MaxReprices:
                    log($"outbid at {placed.Value.UnitPrice} — cancelling to re-post (reprice {attempt}/{options.MaxReprices})");
                    if (!await CancelOrderAsync(orderName)) return null;
                    continue;

                default:
                    // Timed out. Anything already filled has been claimed; the remainder is cancelled so the
                    // coins come back instead of sitting in escrow. If some units did land, the flip still
                    // continues with what it holds.
                    log("buy leg timed out — cancelling the remainder");
                    await CancelOrderAsync(orderName);
                    return (placed.Value.UnitPrice, placed.Value.Cost, _unitsBought > 0);
            }
        }
        return null;
    }

    /// <summary>Sells the goods, with the same undercut handling on the offer side.</summary>
    private async Task<(double UnitPrice, double Proceeds, bool Filled)?> LiquidateAsync(FlipOpportunity flip, FlipOptions options)
    {
        var orderName = $"SELL {flip.Name}";
        for (var attempt = 1; attempt <= options.MaxReprices + 1; attempt++)
        {
            var offered = await PlaceSellOfferAsync(flip);
            if (offered is null) return null;

            var outcome = await WatchOrderAsync(orderName, "coins to claim", offered.Value.UnitPrice, isBuy: false, options);
            switch (outcome)
            {
                case OrderOutcome.Claimed:
                    return (offered.Value.UnitPrice, offered.Value.Proceeds, true);

                case OrderOutcome.Outbid when attempt <= options.MaxReprices:
                    log($"undercut at {offered.Value.UnitPrice} — cancelling to re-post (reprice {attempt}/{options.MaxReprices})");
                    if (!await CancelOrderAsync(orderName)) return null;
                    continue;

                default:
                    return (offered.Value.UnitPrice, offered.Value.Proceeds, false);
            }
        }
        return null;
    }

    /// <summary>"Filled: 4/4 (100%)" — true only when both sides of the fraction match.</summary>
    private static bool IsFullyFilled(string status)
    {
        var match = System.Text.RegularExpressions.Regex.Match(status, @"Filled:\s*([\d,]+)\s*/\s*([\d,]+)");
        if (!match.Success) return false;
        return match.Groups[1].Value == match.Groups[2].Value;
    }

    private enum OrderOutcome
    {
        Claimed,
        Outbid,
        TimedOut,
        Failed
    }

    /// <summary>
    /// Polls the order manager until the order pays out, or until the live book says we have been undercut for
    /// long enough that re-posting beats waiting.
    /// </summary>
    private async Task<OrderOutcome> WatchOrderAsync(string orderName, string claimPhrase, double ourPrice, bool isBuy, FlipOptions options)
    {
        log($"--- watching \"{orderName}\" @ {ourPrice} ({claimPhrase}), up to {options.MonitorMinutes} min ---");
        var deadline = DateTime.UtcNow.AddMinutes(options.MonitorMinutes);
        DateTime? outbidSince = null;
        var poll = 0;

        while (DateTime.UtcNow < deadline)
        {
            poll++;
            if (!session.Client.IsConnected)
            {
                log("disconnected while waiting");
                return OrderOutcome.Failed;
            }

            if (!await EnsureUsableHubAsync(options))
            {
                log($"poll {poll}: hub is unusable and recovery failed");
                return OrderOutcome.Failed;
            }

            if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders"))
            {
                log($"poll {poll}: could not open the order manager, retrying");
                await session.CloseAsync();
                await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
                continue;
            }

            await session.WaitForMenuContentAsync(TimeSpan.FromSeconds(5));

            var order = session.FindSlot(orderName);
            if (order is null)
            {
                // Never conclude anything from one empty read. The order manager arrives over several packets,
                // and a snapshot taken mid-fill shows no rows at all — which once reported a completed flip as
                // a 5,201-coin loss while the offer was still live with the goods in escrow. Look again, and
                // only believe a disappearance the ledger can explain.
                await session.CloseAsync();
                await Task.Delay(2000);

                var stillMissing = true;
                if (await session.OpenNpcMenuAsync("Bazaar") && await session.ClickAsync("Manage Orders"))
                {
                    await session.WaitForMenuContentAsync(TimeSpan.FromSeconds(5));
                    stillMissing = session.FindSlot(orderName) is null;
                }
                await session.CloseAsync();

                if (!stillMissing)
                {
                    log($"poll {poll}: \"{orderName}\" reappeared on a second look — the first read was mid-refresh");
                    await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
                    continue;
                }

                var settled = isBuy ? _unitsBought > 0 : _unitsSold > 0;
                log($"poll {poll}: \"{orderName}\" is genuinely gone; ledger says it {(settled ? "settled" : "did NOT settle")}");
                return settled ? OrderOutcome.Claimed : OrderOutcome.Failed;
            }

            var status = order.Lore.FirstOrDefault(l => l.Contains("Filled:", StringComparison.OrdinalIgnoreCase)) ?? "unfilled";
            var claimable = order.Lore.Any(l => l.Contains(claimPhrase, StringComparison.OrdinalIgnoreCase));
            await session.CloseAsync();

            if (claimable)
            {
                // A partial fill is claimable too, and claiming it does NOT close the order — the rest keeps
                // working. Selling on the first partial would offer 1 unit while 3 were still on order and
                // make the run's own accounting nonsense, so collect and carry on waiting.
                var complete = IsFullyFilled(status);
                log($"poll {poll}: {status} <- claiming{(complete ? "" : " (partial; order stays live)")}");
                if (!await ClaimOrderAsync(orderName)) return OrderOutcome.Failed;
                if (complete) return OrderOutcome.Claimed;
                await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
                continue;
            }

            // The live book decides whether waiting is still the right move. A partially filled order is left
            // alone: cancelling it would hand back a position that is already working.
            var partiallyFilled = status.Contains("Filled:") && !status.Contains(" 0/");
            var (bid, ask) = await LiveBookAsync(_productKey);
            var beaten = isBuy ? bid > ourPrice + 0.001 : ask is > 0 && ask < ourPrice - 0.001;

            if (beaten && !partiallyFilled)
            {
                outbidSince ??= DateTime.UtcNow;
                var waited = (DateTime.UtcNow - outbidSince.Value).TotalSeconds;
                log($"poll {poll}: {status}; book is now bid {bid} / ask {ask} — {(isBuy ? "outbid" : "undercut")} for {waited:F0}s");
                if (waited >= options.RepricePatienceSeconds) return OrderOutcome.Outbid;
            }
            else
            {
                if (outbidSince is not null) log($"poll {poll}: back at the top of the book");
                outbidSince = null;
                log($"poll {poll}: {status}; still best (bid {bid} / ask {ask})");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
        }

        log($"gave up waiting for \"{orderName}\"");
        return OrderOutcome.TimedOut;
    }

    private async Task<bool> ClaimOrderAsync(string orderName)
    {
        if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders")) return false;

        var chatMark = session.ChatCount;
        await session.ClickAsync(orderName);
        await Task.Delay(2000);
        foreach (var line in session.ChatSince(chatMark).Where(l => l.Contains("Claim", StringComparison.OrdinalIgnoreCase)))
        {
            log($"  {line}");
            RecordClaim(line);
        }
        await session.CloseAsync();
        return true;
    }

    /// <summary>Right-click an order for its options screen, then cancel it — the coins or goods come back.</summary>
    private async Task<bool> CancelOrderAsync(string orderName)
    {
        // Retried as a whole: a GUI that closes under us mid-sequence is recoverable by starting the sequence
        // again, and giving up here would strand an order (and its escrowed coins) on the Bazaar.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (await TryCancelOrderAsync(orderName)) return true;
            log($"cancel attempt {attempt}/3 failed; retrying");
            await session.CloseAsync();
            await Task.Delay(1500);
        }
        return false;
    }

    private async Task<bool> TryCancelOrderAsync(string orderName)
    {
        if (!await session.OpenNpcMenuAsync("Bazaar") || !await session.ClickAsync("Manage Orders")) return false;
        if (!await session.ClickAsync(orderName, button: 1)) return false;

        if (!session.ContainerTitle.Contains("Order options", StringComparison.OrdinalIgnoreCase))
        {
            log($"expected \"Order options\", got \"{session.ContainerTitle}\"");
            await session.CloseAsync();
            return false;
        }

        var chatMark = session.ChatCount;
        await session.ClickAsync("Cancel Order");
        await Task.Delay(2500);
        var cancelled = session.ChatSince(chatMark).Any(l => l.Contains("Cancelling order", StringComparison.OrdinalIgnoreCase));
        foreach (var line in session.ChatSince(chatMark)) log($"  {line}");
        await session.CloseAsync();

        if (!cancelled) log("no cancellation confirmation in chat");
        return cancelled;
    }

    /// <summary>Live top of book straight from BazaarCompanion — cheaper than re-opening the product page.</summary>
    private async Task<(double Bid, double Ask)> LiveBookAsync(string productKey)
    {
        try
        {
            var product = await api.GetFromJsonAsync<LiveProduct>($"/api/bot/products/{productKey}");
            return product is null ? (0, 0) : (product.BidPrice, product.AskPrice);
        }
        catch (Exception ex)
        {
            log($"live book lookup failed ({ex.Message}) — assuming we are still best");
            return (0, 0);
        }
    }

    private string _productKey = "";
    private FlipOptions _options = null!;

    // The ledger is built only from Hypixel's own claim messages, so partial fills, re-prices and any
    // difference between the quoted and executed price are all accounted for without anyone interpreting
    // anything. Nothing here is inferred from what we intended to do.
    private int _unitsBought;
    private double _coinsSpent;
    private int _unitsSold;
    private double _coinsReceived;

    private static readonly System.Text.RegularExpressions.Regex ClaimedItems =
        new(@"Claimed\s+([\d,]+)x\s+.*?bought for\s+([\d,]+(?:\.\d+)?)\s+each",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex ClaimedCoins =
        new(@"Claimed\s+([\d,]+(?:\.\d+)?)\s+coins from selling\s+([\d,]+)x",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static double Num(string text) =>
        double.TryParse(text.Replace(",", ""), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>Folds a claim message into the ledger. Returns true if the line was a claim we understood.</summary>
    private bool RecordClaim(string line)
    {
        var items = ClaimedItems.Match(line);
        if (items.Success)
        {
            var qty = (int)Num(items.Groups[1].Value);
            var unit = Num(items.Groups[2].Value);
            _unitsBought += qty;
            _coinsSpent += qty * unit;
            log($"  ledger: +{qty} units at {unit} (bought {_unitsBought}, spent {_coinsSpent:F1})");
            return true;
        }

        var coins = ClaimedCoins.Match(line);
        if (coins.Success)
        {
            var received = Num(coins.Groups[1].Value);
            var qty = (int)Num(coins.Groups[2].Value);
            _unitsSold += qty;
            _coinsReceived += received;
            log($"  ledger: -{qty} units for {received} coins (sold {_unitsSold}, received {_coinsReceived:F1})");
            return true;
        }

        return false;
    }

    private sealed record LiveProduct(
        [property: JsonPropertyName("bidPrice")] double BidPrice,
        [property: JsonPropertyName("askPrice")] double AskPrice);

    /// <summary>
    /// Re-opens the order manager every poll until the named order has something to collect, then claims it.
    /// This is the part that needs the bot to stay in the world: an order priced inside the spread fills when
    /// somebody else trades into it, which can be seconds or can be many minutes.
    /// </summary>
    private static double? ParseCoins(string? loreLine)
    {
        if (loreLine is null) return null;
        var match = System.Text.RegularExpressions.Regex.Match(loreLine, @"([\d,]+(?:\.\d+)?)\s*coins");
        if (!match.Success) return null;
        return double.TryParse(match.Groups[1].Value.Replace(",", ""),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
