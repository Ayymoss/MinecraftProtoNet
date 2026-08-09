using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Bazaar.Trading;
using MinecraftProtoNet.Core.Utilities;

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
    int MaxFillMinutes,
    /// <summary>
    /// Join, walk to the Bazaar and then do nothing but stand and fidget — never open a container, never
    /// trade. The control arm for the lobby-ejection investigation: the kick message ("Sending packets too
    /// fast!") is the same whether or not the bot is trading, and fifteen of eighteen kicks happened at an
    /// outbound rate BELOW a real client's average, so the rate cannot be what provoked them. Running an
    /// account that produces only the baseline stream separates "our connection is the problem" from "what
    /// the bot does over it is the problem" — which no amount of re-reading the trading logs can.
    /// </summary>
    bool ControlOnly = false,
    /// <summary>
    /// Pick the EMPTIEST joinable hub instead of the busiest, ignoring <see cref="MinHubPlayers"/>.
    ///
    /// Deliberately trades away blending-in. A crowded hub means thousands of inbound entity packets a second;
    /// if the ejections scale with how much the connection has to carry, an empty hub is the cleanest way to
    /// see it. A vanilla client idling in the same hubs is never kicked, so the difference is ours — and the
    /// inbound load is one of the few things that differs between an idle bot and an idle player.
    /// </summary>
    bool PreferEmptyHub = false,
    /// <summary>
    /// Commands to run once, after the session has settled — semicolon separated, no leading slash.
    /// For one-off account chores (accepting a friend request, setting a toggle) that would otherwise need a
    /// human to log the account in and undo everything the bot is in the middle of.
    /// </summary>
    string? StartupCommands = null,
    /// <summary>
    /// Where the control arm should stand while idling, as "x,y,z". Walked to once, after the hub is chosen.
    ///
    /// Lets the control arm sit on a KNOWN block so a human can go and watch it, and so every run idles in the
    /// same place — a control that stands somewhere different each time varies its surroundings (entity
    /// density, nearby players) along with everything else.
    /// </summary>
    string? IdleAt = null,
    /// <summary>
    /// Control arm variant: drive the Auction House menus at the trading arm's cadence instead of standing
    /// still. Separates "any container interaction at this rate" from "something specific to the Bazaar" —
    /// see AuctionStressPassAsync.
    /// </summary>
    bool AuctionStress = false,
    /// <summary>Which ingredient of an Auction-House pass to exercise: open | click | sign | full.</summary>
    string StressMode = "full",
    /// <summary>Seconds between passes. Lower values shorten time-to-kick and sharpen the comparison.</summary>
    int StressGapSeconds = 45);

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

    /// <summary>
    /// What the Bazaar itself calls this product, which is not always what the API calls it — the API says
    /// "Shard Foxtrot" where the game says "Foxtrot Shard", and the same inversion applies to enchantments.
    /// Order rows are labelled with the GAME's name, so this is the name that has to be matched against them:
    /// looking for "BUY Shard Foxtrot" when Hypixel wrote "BUY Foxtrot Shard" would read as our own order
    /// having vanished, which the recovery logic treats as a settled leg.
    /// </summary>
    public string Name { get; set; } = name;
    public int Quantity { get; } = quantity;
    public double EntryMarginPerUnit { get; } = entryMarginPerUnit;

    public PositionSide Side { get; set; } = PositionSide.Buying;
    public double OrderPrice { get; set; }
    public double LegEntryPrice { get; set; }

    /// <summary>
    /// When the whole flip began, as distinct from <see cref="LegStarted"/> which resets when the buy leg
    /// becomes a sell leg. Scoring a product needs the round-trip duration — capital is tied up for both legs.
    /// </summary>
    public DateTime Opened { get; set; } = DateTime.UtcNow;
    public DateTime LegStarted { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the flip finished. Null while the position is still live, and null for positions rehydrated from
    /// files written before this existed — the page shows those as unknown rather than inventing a time,
    /// because a made-up close time would silently distort the realised-P&amp;L timeline.
    /// </summary>
    public DateTime? ClosedAt { get; set; }
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

    /// <summary>
    /// False for a position rebuilt from the order menu alone, where the true cost is unknowable — the row
    /// shows only its own price. Such a position still trades normally, but its profit is a guess, so it is
    /// reported apart from the session's realised P&amp;L rather than quietly added to it.
    /// </summary>
    public bool BasisKnown { get; set; } = true;

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

    /// <summary>
    /// Realised P&amp;L bucketed into 30-minute slots across the last 24 hours, oldest first.
    ///
    /// Buckets are fixed wall-clock windows rather than "per trade", so a quiet hour reads as a flat run of
    /// zeros instead of vanishing — which is the point of looking at it over a day. Only positions with a
    /// known cost basis count, matching the realised figure shown everywhere else; a guessed basis would put
    /// invented profit on the chart. Positions closed before ClosedAt was recorded are skipped rather than
    /// dumped into the oldest bucket.
    /// </summary>
    private static List<object> BuildPnlSeries(IEnumerable<Position> closed)
    {
        const int bucketMinutes = 30;
        const int buckets = 48; // 24 hours

        var now = DateTime.UtcNow;
        var end = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute / bucketMinutes * bucketMinutes, 0,
            DateTimeKind.Utc).AddMinutes(bucketMinutes);
        var start = end.AddMinutes(-bucketMinutes * buckets);

        var totals = new double[buckets];
        var counts = new int[buckets];

        foreach (var p in closed)
        {
            if (!p.BasisKnown || p.ClosedAt is not { } t || t < start || t >= end) continue;
            var index = (int)((t - start).TotalMinutes / bucketMinutes);
            if (index < 0 || index >= buckets) continue;
            totals[index] += p.Profit;
            counts[index]++;
        }

        var series = new List<object>(buckets);
        for (var i = 0; i < buckets; i++)
        {
            series.Add(new
            {
                at = start.AddMinutes(bucketMinutes * i),
                profit = totals[i],
                trades = counts[i]
            });
        }
        return series;
    }

    /// <summary>Parses an "x,y,z" idle spot. Returns null (and the caller idles where it is) on anything odd.</summary>
    private static (int X, int Y, int Z)? ParseIdleAt(string value)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var x)) return null;
        if (!int.TryParse(parts[1], out var y)) return null;
        if (!int.TryParse(parts[2], out var z)) return null;
        return (x, y, z);
    }
    private static readonly (int X, int Y, int Z) BazaarPos = (-36, 72, -28);

    private readonly List<Position> _open = [];

    /// <summary>
    /// Profit actually earned, counting only positions whose cost was measured.
    ///
    /// One definition used everywhere. The status page filtered on <c>BasisKnown</c> while the cycle log and
    /// the session's return value did not, so the same run reported 631,361.5 in one place and 630,670.5 in
    /// another — the difference being an inherited position whose "loss" was measured against a cost we had
    /// invented. Two numbers both claiming to be the profit is worse than either being wrong.
    /// </summary>
    private double RealisedProfit() => _closed.Where(p => p.BasisKnown).Sum(p => p.Profit);

    /// <summary>
    /// Realised profit per hour, over the window in which it was earned.
    ///
    /// The denominator is the span from the OLDEST closed position's opening to now, not this process's
    /// uptime. Profit carries across restarts and the bot restarts often, so uptime would divide two hours of
    /// earnings by two minutes of running and report a rate sixty times the truth. Zero until something has
    /// closed, because a rate over no completed trades is not a small number — it is no number at all.
    /// </summary>
    private static double RealisedPerHour(IReadOnlyCollection<Position> closed)
    {
        var measured = closed.Where(p => p.BasisKnown).ToList();
        if (measured.Count == 0) return 0;

        var since = measured.Min(p => p.Opened);
        var hours = (DateTime.UtcNow - since).TotalHours;

        // A flip that closed seconds after opening would divide by ~zero; floor the window at a minute.
        return measured.Sum(p => p.Profit) / Math.Max(hours, 1.0 / 60);
    }

    /// <summary>Logs to the console and to the status page, so the page needs no separate instrumentation.</summary>
    private void Say(string message)
    {
        log(message);
        status?.Note(message);
    }

    private readonly List<Position> _closed = [];
    private PortfolioOptions _options = null!;

    /// <summary>
    /// Products the Bazaar's own search cannot find under the name the API gives them, plus anything that
    /// failed to open for another reason. Remembered for the session so a cycle never burns itself retrying a
    /// product that has already proved unusable.
    /// </summary>
    private readonly HashSet<string> _unusable = [];

    /// <summary>Products whose API key is unrecoverable, so the "no repricing" notice is given once each.</summary>
    private readonly HashSet<string> _keylessProducts = [];

    /// <summary>What each product has actually earned, decayed over time. Survives restarts.</summary>
    private readonly ProductScorecard _scorecard = ProductScorecard.Load();
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    /// <summary>
    /// Consecutive cycles that could not read the order manager. Reset on any success; when it reaches
    /// <see cref="OrderManagerFailuresBeforeHubSwitch"/> the bot changes hub rather than retrying in place.
    /// </summary>
    private int _orderManagerFailures;

    /// <summary>Ejection times in the last half hour, used to detect the accelerating-kick pattern.</summary>
    private readonly List<DateTime> _recentEjections = [];

    private const int OrderManagerFailuresBeforeHubSwitch = 2;
    private string _hub = "unknown";
    private string _hubServer = "";

    public async Task<bool> RunAsync(PortfolioOptions options)
    {
        _options = options;
        if (status is not null) status.SnapshotProvider = BuildStatusSnapshot;
        session.Subscribe();

        try
        {
            if (!await JoinAndSettleAsync(options)) return false;

            if (!string.IsNullOrWhiteSpace(options.StartupCommands))
            {
                foreach (var cmd in options.StartupCommands.Split(';', StringSplitOptions.RemoveEmptyEntries
                                                                     | StringSplitOptions.TrimEntries))
                {
                    Say($"startup command: /{cmd}");
                    await session.SendCommandAsync(cmd);

                    // Spaced out because the chat sink throttles anyway, and a burst of commands on join is
                    // exactly the shape of traffic this whole investigation is trying to keep clean.
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }

                foreach (var line in session.ChatSnapshot().TakeLast(6)) Say($"  chat: {line}");
            }

            if (options.ControlOnly) return await RunControlAsync(options);

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

                // Waited out with idle movement rather than a dead sleep: this is where the bot spends most of
                // its life, and standing motionless at one coordinate for hours is what gives it away.
                await session.IdleAsync(TimeSpan.FromSeconds(options.PollSeconds));
            }

            _state = "finished";
            Report();
            return RealisedProfit() > 0;
        }
        finally
        {
            session.Unsubscribe();
            try { await session.DisconnectAsync(); } catch { /* best-effort */ }
            Say("disconnected");
        }
    }

    /// <summary>
    /// The control arm: stand at the Bazaar and never touch a container.
    ///
    /// Reports survival time between ejections rather than profit. If this arm is ejected at the same rate as
    /// the trading arm, every menu-interaction theory is dead and the fault is in the connection itself; if it
    /// survives indefinitely, the trigger is something the trading arm does, and the surviving suspects are
    /// few enough to bisect.
    /// </summary>
    private async Task<bool> RunControlAsync(PortfolioOptions options)
    {
        _state = "control: idling at the Bazaar, no container interaction";
        var joinedAt = DateTime.UtcNow;
        var ejections = 0;
        var survivals = new List<double>();
        var hardStop = DateTime.UtcNow.AddMinutes(options.TradingMinutes + options.WindDownMinutes);

        Say("CONTROL ARM: no trading, no container will be opened. Measuring time-to-ejection only.");

        // Optional one-off hub selection, to put this account on a NAMED server tier.
        //
        // The two accounts land on different tiers by default — the established one on `mega*`, the fresh one
        // on `mini*` — which is confounded with the account itself. The Hub Selector lists the server for each
        // hub, so choosing one separates "this account is treated differently" from "this server tier sheds
        // clients". It costs a single container open at the very start; that cannot explain an ejection twenty
        // minutes later, since the idle arm with ZERO containers still took 19.6 minutes to be kicked.
        if (options.HubNumber > 0)
        {
            // Park on a NAMED hub so a human can stand next to the bot and watch it.
            //
            // There is no `/hub <n>` command on Hypixel — the only way to choose a specific hub is the Hub
            // Selector NPC. That costs exactly one container open at the very start, the same concession the
            // player-count path below already makes, and it cannot explain an ejection twenty minutes later.
            Say($"control arm: selecting Hub #{options.HubNumber} via the Hub Selector so the run can be watched");
            if (await GoToNamedHubAsync(options.HubNumber))
                Say($"control arm settled on {_hub} (server {_hubServer})");
            else
                Say($"control arm could not reach Hub #{options.HubNumber} — idling where it is");
        }
        else if (options.MinHubPlayers > 0)
        {
            // Deliberately the SAME selection the trading arm uses, so the two arms sit in comparably busy
            // hubs. If the control arm picked quiet hubs it would differ from the trading arm in population
            // as well as in container activity, and a difference in ejection rate could not be attributed to
            // either one.
            Say($"control arm: selecting a hub with at least {options.MinHubPlayers} players, same logic as the trading arm");
            if (await GoToBusyHubAsync(options, mustSwitch: true))
                Say($"control arm settled on {_hub} (server {_hubServer})");
            else
                Say("control arm could not pick a hub — idling where it is");
        }

        // Stand on a fixed block, so successive runs idle in identical surroundings and a human can find it.
        if (options.IdleAt is { } idleAt && ParseIdleAt(idleAt) is { } spot)
        {
            Say($"control arm: walking to the idle spot ({spot.X:F0},{spot.Y:F0},{spot.Z:F0})");
            if (await session.WalkToAsync(spot, 120))
                Say("control arm reached the idle spot — standing here, no further menus");
            else
                Say("control arm could not reach the idle spot — idling where it is");
            session.StopMoving();
        }

        while (DateTime.UtcNow < hardStop)
        {
            if (session.Intercepted)
            {
                Say("intercept detected — stopping the control arm");
                break;
            }

            if (session.LobbyEjection is { } ejection)
            {
                var lived = (DateTime.UtcNow - joinedAt).TotalMinutes;
                survivals.Add(lived);
                ejections++;

                // Tell the status page too. Only the trading arm was reporting ejections, so this arm's page
                // showed an ever-growing "clean" time while the bot was in fact being kicked — the one number
                // the experiment is judged on, silently wrong on the only screen a human can see.
                status?.NoteEjection();
                _stressStarted = DateTime.UtcNow;

                Say($"CONTROL ejection #{ejections} after {lived:F1} min alive — reason: {ejection} " +
                    $"[mode={_options?.StressMode ?? "-"}, passes={_auctionPasses}]");

                // Recovery is a bare /hub rather than the trading arm's EnsureUsableHubAsync, because that
                // opens the Hub Selector container. Being ejected already put us in the lobby, so a warp is
                // all that is needed to get back to a hub and resume producing baseline traffic.
                session.ClearLobbyEjection();
                await session.SendCommandAsync("hub");
                await Task.Delay(TimeSpan.FromSeconds(10));

                if (!session.IsConnected)
                {
                    Say("control arm did not survive the warp back — stopping");
                    break;
                }

                joinedAt = DateTime.UtcNow;
                continue;
            }

            if (!session.IsConnected)
            {
                Say("control arm lost the connection outright — stopping");
                break;
            }

            if (options.AuctionStress)
            {
                await AuctionStressPassAsync();
            }
            else
            {
                await session.IdleAsync(TimeSpan.FromSeconds(options.PollSeconds));
            }
        }

        var alive = (DateTime.UtcNow - joinedAt).TotalMinutes;
        var mean = survivals.Count > 0 ? survivals.Average() : alive;
        Say($"CONTROL RESULT: {ejections} ejection(s) over {(DateTime.UtcNow - _startedUtc).TotalMinutes:F1} min; " +
            $"mean survival {mean:F1} min; still alive {alive:F1} min at stop");
        _state = $"control finished: {ejections} ejection(s), mean survival {mean:F1} min";
        return ejections == 0;
    }

    // ===== Session setup =====

    /// <summary>
    /// Joins, gets to a busy hub, walks to the Bazaar and takes ownership of whatever is already trading.
    ///
    /// Retried, because Hypixel's kick to the lobby lands during this walk as often as anywhere else and a
    /// single failure here used to end the entire run: the recovery lived only inside the cycle loop, which a
    /// failed join never reaches. A session that gives up forty seconds after starting leaves every live
    /// order unmanaged, which is far worse than walking to the Bazaar twice.
    /// </summary>
    private async Task<bool> JoinAndSettleAsync(PortfolioOptions options)
    {
        if (!await session.ConnectAndSpawnAsync(options.Server, options.Port))
        {
            Say("CONNECT/SPAWN FAILED");
            return false;
        }

        Say("connected + spawned");
        status?.NoteConnected();
        await session.SelectEmptyHotbarSlotAsync();

        // Warp only if we are not already where we want to be.
        //
        // The bot logs back in exactly where it left off — a SkyBlock hub — and then used to warp twice
        // anyway: /skyblock one second after login, /hub eight seconds later. Each is a backend transfer, and
        // reconnecting every few minutes turned that into a steady churn of server switches no real player
        // produces. The sidebar already says where we are, so read it instead of guessing.
        var sidebar = await session.WaitForSidebarAsync(TimeSpan.FromSeconds(8));
        if (sidebar.Count > 0) Say($"  sidebar on join: {string.Join(" | ", sidebar.Take(6))}");

        var inSkyBlock = sidebar.Any(l => l.Contains("SKYBLOCK", StringComparison.OrdinalIgnoreCase)
                                          || l.Contains("⏣", StringComparison.Ordinal));
        var inHub = sidebar.Any(l => l.Contains("Hub", StringComparison.OrdinalIgnoreCase));

        if (!inSkyBlock)
        {
            Say("  not in SkyBlock — warping in");
            await session.SendCommandAsync("skyblock");
            await Task.Delay(TimeSpan.FromSeconds(9));
        }
        else
        {
            Say("  already in SkyBlock — skipping the /skyblock warp");
        }

        if (!inHub)
        {
            Say("  not in a hub — warping to one");

            // Guarded because an ejection can land DURING startup, between the hub check and the warp. The
            // send then throws "Output stream not available" out of RunAsync and kills the whole process —
            // which reads as "the bot went quiet" rather than "the bot was kicked", and in the bisect harness
            // stalled a run for its full timeout waiting for an ejection that could never be logged.
            if (!session.IsConnected)
            {
                Say("  lost the connection before the warp — letting the caller retry");
                return false;
            }

            try
            {
                await session.SendCommandAsync("hub");
            }
            catch (InvalidOperationException ex)
            {
                Say($"  /hub failed, connection is gone ({ex.Message}) — letting the caller retry");
                return false;
            }

            await Task.Delay(TimeSpan.FromSeconds(6));
        }
        else
        {
            Say("  already in a hub — skipping the /hub warp");
        }

        await session.SelectEmptyHotbarSlotAsync();

        // The control arm stops here, in whatever hub /hub chose.
        //
        // It deliberately does NOT go on to pick a busy hub or walk to the Bazaar: choosing a hub means
        // opening the Hub Selector, which is a container, and a control that opens containers is not a
        // control. Where it stands does not matter either — the quiet ejections happen while idling, not
        // while trading — so the baseline packet stream is the whole of what this arm is here to produce.
        if (options.ControlOnly)
        {
            Say("CONTROL: settled in the hub /hub chose; no hub selection, no Bazaar walk, no containers");
            return true;
        }

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            if (session.Intercepted) return false;

            // Noted BEFORE the call, because recovering clears the flag. When there was an ejection to clear,
            // EnsureUsableHubAsync does the ENTIRE recovery — pick a busy hub, walk to the Bazaar — and
            // leaves us standing at the NPC. Repeating that walk here is not merely wasteful, it fails: the
            // Hub Selector is forty blocks behind us by then and cannot be found within its search window, so
            // the attempt was reported as "did not reach the Bazaar" while the bot stood at the Bazaar. Four
            // of those ended the run.
            var recovering = session.LobbyEjection is not null;

            if (!await EnsureUsableHubAsync())
            {
                Say($"join attempt {attempt}/4: could not settle in a hub — retrying");
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            if (recovering || (await GoToBusyHubAsync(options) && await WalkToBazaarAsync()))
            {
                // The control arm must not open a container even once, or it stops being a control.
                if (options.ControlOnly) return true;

                await AdoptExistingOrdersAsync();

                // Done after adoption so it only ever sees stock that genuinely has no position behind it.
                await AdoptUntrackedStockAsync();
                return true;
            }

            Say($"join attempt {attempt}/4 did not reach the Bazaar — retrying");
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        Say("could not reach the Bazaar after four attempts");
        return false;
    }

    /// <summary>
    /// Takes ownership of orders that are already live on the account.
    ///
    /// Orders outlive the process: a crash, a restart, or a session that ended while positions were open all
    /// leave real coins and goods sitting on the Bazaar. Without this the bot would ignore them, commit its
    /// capital again on top, and leave the originals for a human to find. Cost basis is deliberately left at
    /// zero — it gets filled in from Hypixel's own claim messages when the order pays out, which is the same
    /// source the rest of the ledger uses.
    /// </summary>
    private async Task AdoptExistingOrdersAsync()
    {
        // The ledger is read FIRST, and unconditionally.
        //
        // This used to sit below the "no rows" early return, which made loading history conditional on the
        // order menu being readable while SAVING it stayed unconditional — every cycle calls
        // PositionStore.Save(_open, _closed). One startup where the menu came back empty or null therefore
        // left _closed empty and the next save wrote that emptiness over the whole file. Observed
        // 2026-08-09: 203 closed flips replaced by 2, silently, with no error anywhere in the log because
        // nothing had failed. Any load that a save can outrun will eventually destroy the data.
        var state = PositionStore.Load(Say);

        // Completed flips come back too, so the running profit shown on the monitor is the profit for the
        // whole run of the bot rather than for whatever fraction of it this process happened to see.
        foreach (var snapshot in state.Closed ?? [])
        {
            _closed.Add(PositionStore.Rehydrate(snapshot));
        }

        if (_closed.Count > 0)
            Say($"  carried forward {_closed.Count} closed position(s), realised {_closed.Where(p => p.BasisKnown).Sum(p => p.Profit):N1}");

        var rows = await ReadOrdersAsync();
        if (rows is null || rows.Count == 0)
        {
            Say("  no order rows to adopt — keeping the carried-forward history");
            return;
        }

        // The saved file knows what things cost; the menu knows what is still live. Both are needed, and the
        // menu wins on existence — an order that filled while the process was down must not come back.
        var saved = state.Open;

        foreach (var row in rows)
        {
            var name = row.Name!;
            var isBuy = name.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase);
            var product = name[(isBuy ? 4 : 5)..].Trim();

            var quantity = (int)(ReadLoreNumber(row, isBuy ? "Order amount:" : "Offer amount:") ?? 0);
            var price = ReadLoreNumber(row, "Price per unit:") ?? 0;
            if (quantity <= 0 || price <= 0)
            {
                Say($"  adopting \"{name}\" is not possible — could not read its amount or price; leaving it alone");
                continue;
            }

            var wantedSide = isBuy ? nameof(PositionSide.Buying) : nameof(PositionSide.Selling);

            // Matched on NAME, with the same side merely preferred. Requiring the side to agree looked tidier
            // and was expensive: a leg that flipped from buying to selling since the last save no longer
            // matched its own record, fell through to the fabricated-basis path, and took its sell price as
            // its cost. That turned a Dreadwing Shard flip bought for 150,689 and sold for 401,423 into a
            // recorded LOSS of 4,567. The menu is the authority on which leg an order is; the file is only
            // ever the authority on what it cost.
            var match = saved.FirstOrDefault(s =>
                             string.Equals(s.Name, product, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(s.Side, wantedSide, StringComparison.OrdinalIgnoreCase))
                         ?? saved.FirstOrDefault(s =>
                             string.Equals(s.Name, product, StringComparison.OrdinalIgnoreCase));

            Position position;
            if (match is not null)
            {
                // Restored with its real product key and real cost, so it can be repriced and its profit is a
                // fact rather than an inference.
                position = new Position(match.ProductKey, match.Name, match.Quantity, match.EntryMarginPerUnit)
                {
                    Side = isBuy ? PositionSide.Buying : PositionSide.Selling,
                    OrderPrice = price,
                    LegEntryPrice = match.LegEntryPrice,
                    LegStarted = match.LegStarted,
                    Steps = match.Steps,
                    UnitsBought = match.UnitsBought,
                    CoinsSpent = match.CoinsSpent,
                    UnitsSold = match.UnitsSold,
                    CoinsReceived = match.CoinsReceived,
                    OrderLive = true,
                    BasisKnown = match.BasisKnown,
                    Opened = match.Opened ?? match.LegStarted
                };

                // The goods are demonstrably ours — they are on offer — but the record was saved before the
                // buy filled, so what they cost was never written down. Say so rather than inventing a figure:
                // an assumed basis both misreports the profit and, via the cost floor, can strand the offer.
                if (!isBuy && position.UnitsBought == 0)
                {
                    position.UnitsBought = quantity;
                    position.BasisKnown = false;
                    Say($"  RESUMED {name} x{quantity} @ {price} — held, but its cost was never recorded; " +
                        "trading it out without counting the profit");
                }
                else
                {
                    Say($"  RESUMED {name} x{quantity} @ {price} (cost {position.CostPerUnit:N1}/unit, key {match.ProductKey})");
                }
            }
            else
            {
                // No saved cost, but the key is still recoverable from the display name, and without it this
                // position could never be repriced for the rest of its life.
                var key = await ResolveProductKeyAsync(product) ?? product;

                position = new Position(key, product, quantity, 0)
                {
                    Side = isBuy ? PositionSide.Buying : PositionSide.Selling,
                    OrderPrice = price,
                    LegEntryPrice = price,
                    OrderLive = true,
                    BasisKnown = false
                };

                // A sell offer means the goods were already bought; without a real cost basis the never-sell-
                // below-cost floor has nothing to protect, so treat the offer price as the basis. That is
                // conservative: it can only stop the bot cutting the price, never encourage it.
                if (!isBuy)
                {
                    position.UnitsBought = quantity;
                    position.CoinsSpent = quantity * price;
                }

                Say($"  ADOPTED {name} x{quantity} @ {price} — no saved cost, so its profit will not be counted");
            }

            _open.Add(position);
        }

        // Goods held with NO order on the book cannot be rebuilt from the order menu, because the menu has
        // nothing to say about them — and those are exactly the positions most at risk of being forgotten. It
        // happens whenever a sell offer fails to go up (an ejection mid-flow will do it), leaving items in the
        // inventory that nothing is trying to sell. Five Hummingbird Shards worth 133,355 sat orphaned this
        // way. Restored here from the saved state so the servicing loop lists them.
        var stranded = saved.Where(s =>
            s.UnitsBought > s.UnitsSold &&
            !_open.Any(p => string.Equals(p.Name, s.Name, StringComparison.OrdinalIgnoreCase)));

        foreach (var s in stranded)
        {
            var held = s.UnitsBought - s.UnitsSold;
            _open.Add(new Position(s.ProductKey, s.Name, s.Quantity, s.EntryMarginPerUnit)
            {
                Side = PositionSide.Selling,
                LegEntryPrice = s.LegEntryPrice,
                LegStarted = DateTime.UtcNow,
                Opened = s.Opened ?? s.LegStarted,
                UnitsBought = s.UnitsBought,
                CoinsSpent = s.CoinsSpent,
                UnitsSold = s.UnitsSold,
                CoinsReceived = s.CoinsReceived,
                BasisKnown = s.BasisKnown,
                OrderLive = false
            });

            Say($"  RECOVERED {s.Name}: {held} held with no offer on the book (cost {s.CoinsSpent:N1}) — will re-list");
        }

        ReportUntrackedStock();

        PositionStore.Save(_open, _closed, Say);
    }

    /// <summary>
    /// Turns a display name from the in-game order menu into the API's product key.
    ///
    /// The key cannot be derived from the name: the API inverts word order for some families ("Shard Foxtrot"
    /// for the game's "Foxtrot Shard") and irregulars like ENCHANTED_SLIME_BALL follow no rule at all. Null
    /// when the API cannot place it, which costs repricing for that position and nothing else.
    /// </summary>
    /// <summary>
    /// Items that are part of the SkyBlock UI rather than the player's stock, so they must never be looked up
    /// or sold.
    ///
    /// The SkyBlock Menu is a fixed, unmovable, unremovable item in the last hotbar slot of every player on
    /// the server — it is a menu button, not loot. Asking the Bazaar API about it returns 404 every time, and
    /// because the inventory sweep runs on every cycle that is a guaranteed failing request (and a confusing
    /// log line) for the entire life of the run. Matched on a name prefix because the game appends a hint to
    /// the display name, e.g. "SkyBlock Menu (Click)".
    /// </summary>
    private static readonly string[] NonTradableUiItems = ["SkyBlock Menu"];

    private static bool IsUiItem(string displayName) =>
        NonTradableUiItems.Any(x => displayName.StartsWith(x, StringComparison.OrdinalIgnoreCase));

    private async Task<string?> ResolveProductKeyAsync(string displayName)
    {
        if (IsUiItem(displayName)) return null;

        try
        {
            var matches = await api.GetFromJsonAsync<List<ProductKeyMatch>>(
                $"/api/bot/products/lookup?name={Uri.EscapeDataString(displayName)}");

            var key = matches?.FirstOrDefault()?.ProductKey;
            if (key is not null) Say($"  \"{displayName}\" resolves to {key}");
            return key;
        }
        catch (Exception ex)
        {
            Say($"  could not resolve a product key for \"{displayName}\" ({ex.Message})");
            return null;
        }
    }

    private sealed record ProductKeyMatch(string ProductKey, string Name);

    /// <summary>
    /// Compares what is actually in the bot's inventory against what it believes it is holding, and says so.
    ///
    /// Every accounting bug in this system ends the same way — goods in the bag that no position is trying to
    /// sell — and until now the only way to notice was for a human to look. A position closed in error, a sell
    /// offer that never went up, a claim that landed after the position was dropped: all of them leave stock
    /// behind, and stock nobody is selling is money that stops working. Reported rather than acted on, because
    /// the inventory also holds ordinary things the bot never bought.
    /// </summary>
    private void ReportUntrackedStock()
    {
        var entity = session.Client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        // Main inventory and hotbar; armour and the crafting grid hold nothing we trade.
        var held = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (index, slot) in entity.Inventory.Items)
        {
            if (index < 9 || index > 44 || slot.IsEmpty) continue;

            var name = ItemTextHelper.GetDisplayName(slot) ?? ItemTextHelper.GetItemName(slot);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var clean = ItemTextHelper.StripFormattingCodes(name).Trim();
            held[clean] = held.GetValueOrDefault(clean) + Math.Max(slot.ItemCount, 1);
        }

        if (held.Count == 0) return;

        var tracked = _open
            .Where(p => p.UnitsBought > p.UnitsSold)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var untracked = held
            .Where(kv => !tracked.Any(t => kv.Key.Contains(t, StringComparison.OrdinalIgnoreCase)
                                           || t.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)))
            // Drop UI furniture before it is ever reported as unexplained stock. The SkyBlock Menu sits in
            // every player's last hotbar slot permanently, so listing it under "items NO position is selling"
            // is a false alarm on every single cycle.
            .Where(kv => !IsUiItem(kv.Key))
            .ToList();

        if (untracked.Count == 0)
        {
            Say($"  inventory reconciles: {held.Count} stack kind(s) held, all covered by open positions");
            return;
        }

        Say($"  !! {untracked.Count} item kind(s) in the inventory that NO position is selling:");
        foreach (var (name, count) in untracked.OrderByDescending(kv => kv.Value))
        {
            Say($"     {count,4}x {name}");
        }

        _untrackedStock = untracked.OrderByDescending(kv => kv.Value).ToList();
    }

    /// <summary>Stock found in the bag that no position covers, pending an attempt to sell it.</summary>
    private List<KeyValuePair<string, int>> _untrackedStock = [];

    /// <summary>
    /// Turns orphaned stock back into positions so it gets sold.
    ///
    /// Goods sitting in the bag are capital doing nothing, and tonight produced several hundred thousand
    /// coins of it through closed-in-error positions and sell offers that never went up. Only items the API
    /// recognises as Bazaar products are adopted — that filter is what stops the bot listing a menu item, a
    /// quest reward, or anything else it did not buy. Cost is unknown by definition, so these are marked
    /// <c>BasisKnown = false</c>: they trade out at the market price without inventing a profit.
    /// </summary>
    private async Task AdoptUntrackedStockAsync()
    {
        if (_untrackedStock.Count == 0) return;

        foreach (var (name, count) in _untrackedStock)
        {
            if (_open.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) continue;

            // UI furniture, not stock. Skipped silently: it is in every player's inventory permanently, so
            // reporting it every cycle is noise about something that will never change.
            if (IsUiItem(name)) continue;

            var key = await ResolveProductKeyAsync(name);
            if (key is null)
            {
                Say($"  leaving {count}x {name} alone — the Bazaar does not list it");
                continue;
            }

            _open.Add(new Position(key, name, count, 0)
            {
                Side = PositionSide.Selling,
                UnitsBought = count,
                CoinsSpent = 0,
                BasisKnown = false,
                OrderLive = false,
                Opened = DateTime.UtcNow,
                LegStarted = DateTime.UtcNow
            });

            Say($"  SELLING {count}x {name} found loose in the inventory (cost unknown, excluded from P&L)");
        }

        _untrackedStock.Clear();
        PositionStore.Save(_open, _closed, Say);
    }

    /// <summary>Pulls a number out of a lore line like "Order amount: 51,125x" or "Price per unit: 4,000.0 coins".</summary>
    private static double? ReadLoreNumber(MenuSlot row, string label)
    {
        var line = row.Lore.FirstOrDefault(l => l.Contains(label, StringComparison.OrdinalIgnoreCase));
        if (line is null) return null;

        var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d,]+(?:\.\d+)?)");
        return match.Success ? Num(match.Groups[1].Value) : null;
    }

    /// <param name="mustSwitch">
    /// Refuse to stay on the current server, however busy it is. Passed explicitly rather than read from
    /// <c>session.RestartWarningAt</c> because the caller clears that flag BEFORE calling this — so the check
    /// always saw null and the bot stayed on a rebooting hub. Observed at the 00:38 reboot: it announced
    /// "leaving this hub (restart warning)", then reported "busy enough" at 38/60 and stayed put. Only an
    /// unrelated ejection moved it off the server before it went down.
    /// </param>
    /// <summary>Where the Auction Agent stands in the SkyBlock hub.</summary>
    // The ground-level Auction Master beside the Bazaar, not the Auction Agent on the upper platform at
    // (-34,82,-13): all three auction NPCs open the same menus, and Baritone stalls ten blocks short of the
    // raised one. Position from the skyblock-bazaar NPC recon.
    private static readonly (int X, int Y, int Z) AuctionNpcPos = (-39, 73, -12);

    /// <summary>
    /// Safe toggles inside "Auctions Browser". All are view filters that redraw the page — none bid, buy or
    /// list anything. "Create Auction" is deliberately absent, and ClickAsync's commit guard refuses it anyway.
    /// </summary>
    private static readonly string[] AuctionToggles = ["Sort", "Item Tier", "BIN Filter"];

    /// <summary>Search terms cycled through the sign editor. Kept short so none is clipped by the 90px line.</summary>
    private static readonly string[] AuctionSearches = ["Drill", "Potion", "Sword", "Talisman", "Boots"];

    private int _auctionPasses;
    private DateTime _stressStarted = DateTime.UtcNow;
    private readonly Random _jitterGap = new();

    /// <summary>
    /// One pass of the Auction House, in one of several MODES, so the kick can be bisected.
    ///
    /// The A/B already showed that menu activity alone is enough: an account that only navigates the Auction
    /// House — no trading, no Bazaar, level 1 — is ejected in 4.7 minutes, while the same client standing
    /// still is untouched for 45+. What that cannot say is WHICH part of a pass does it. These modes each
    /// strip the pass down to one ingredient so time-to-kick can be compared:
    ///
    ///   open   — right-click the NPC and close. No slot is ever clicked.
    ///   click  — open, click navigation entries, close. No sign editor.
    ///   sign   — open, reach the search box, answer it. The fewest clicks that still types.
    ///   full   — everything (the shape the trading arm uses).
    ///
    /// Whichever mode kicks fastest per action is the one carrying the signal.
    /// </summary>
    private async Task AuctionStressPassAsync()
    {
        _auctionPasses++;
        var mode = (_options?.StressMode ?? "full").ToLowerInvariant();
        _state = $"auction stress [{mode}]: pass {_auctionPasses}, {(DateTime.UtcNow - _stressStarted).TotalMinutes:F1}m clean";

        // "arm3" opens the menu three times and then never again, standing still for the rest of the run.
        //
        // Everything measured so far says time dominates and opens are only a weak enabler: 24 opens
        // was ejected at 215s, 9 opens at 260s, 5 opens at 322s, while 40 approach passes with NO open
        // survived the full 14-minute cap. If a handful of opens arms a fuse that then fires on its own
        // schedule, this run is ejected at the usual ~4 minutes despite being idle for most of it. If it
        // survives, the ejection needs *ongoing* container activity and the counter really does drain.
        if (mode == "arm3" && _auctionPasses > 3)
        {
            _state = $"auction stress [arm3]: idle after 3 opens, {(DateTime.UtcNow - _stressStarted).TotalMinutes:F1}m clean";
            await session.IdleAsync(TimeSpan.FromSeconds(20));
            return;
        }

        // "othernpc" runs the same open/close loop against the Hub Selector instead of the Auction agent.
        //
        // Opening a window WITHOUT an Interact would be the ideal discriminator, but every command that
        // does it (/ah, /bz, /sbmenu) is gated behind a booster cookie on these accounts, and cookies
        // cost ~13m in game, so that route is closed. This is the cookie-free substitute: same packets,
        // same cadence, different NPC and a different menu. Ejected ⇒ any container menu does it.
        // Survives ⇒ something is specific to the Auction House window.
        if (mode == "othernpc")
        {
            if (await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20)))
            {
                await session.ApproachNpcAsync();
                // Open and close only — never click an entry, or it would warp us to another hub.
                if (await session.OpenNpcMenuAsync("Hub Selector")) await session.CloseAsync();
                else Say("othernpc: Hub Selector menu did not open");
            }
            else Say("othernpc: could not find the Hub Selector");

            Say($"auction pass {_auctionPasses} [othernpc] done, 1 menu action(s), " +
                $"{(DateTime.UtcNow - _stressStarted).TotalMinutes:F1}m clean");
            await session.IdleAsync(TimeSpan.FromSeconds(Math.Max(3, _options?.StressGapSeconds ?? 45)));
            return;
        }

        // Only walk if we are not already standing at the NPC.
        //
        // This used to re-path on EVERY pass, which quietly made the experiment about locomotion as well as
        // menus: re-walking produced sprint PlayerCommands and a server correction (Accept Teleportation)
        // roughly every five seconds — 4.8x the rate a real client produces while doing the same menu spam
        // standing still. Every arm that has ever been ejected was one that walks; the stationary idle arm
        // was never touched. Staying put unless genuinely displaced separates the two.
        var here = session.Client.State.LocalPlayer?.Entity?.Position;
        var distance = here is null ? double.MaxValue : Math.Sqrt(
            Math.Pow(here.X - AuctionNpcPos.X, 2) + Math.Pow(here.Z - AuctionNpcPos.Z, 2));

        if (distance > 5.0 && !await session.WalkToAsync(AuctionNpcPos, 120))
        {
            Say("auction: could not reach the agent — retrying next pass");
            return;
        }

        if (!await session.FindNpcAsync("Auction", TimeSpan.FromSeconds(20))) return;
        await session.ApproachNpcAsync();

        // "approach" does everything the open arm does EXCEPT open the menu.
        //
        // This is the arm that separates locomotion from containers, and it is needed because the
        // obvious suspects are gone: the close method turned out to be irrelevant (a run that closed
        // exactly like the vanilla capture — Container Click, zero Container Close — was ejected in
        // 2.84 min, while a run sending 12 Container Closes survived 5.4 min), and our Interact is
        // byte-identical to vanilla's. What still separates every ejected arm from the idle arm that
        // survived 52 minutes is that the ejected ones walk to and face the NPC each pass.
        if (mode == "approach")
        {
            Say($"auction pass {_auctionPasses} [approach] done, 0 menu action(s), " +
                $"{(DateTime.UtcNow - _stressStarted).TotalMinutes:F1}m clean");
            await session.IdleAsync(TimeSpan.FromSeconds(Math.Max(3, _options?.StressGapSeconds ?? 45)));
            return;
        }

        if (!await session.OpenNpcMenuAsync("Auction"))
        {
            Say($"auction: menu did not open on pass {_auctionPasses}");
            return;
        }

        var actions = 1;   // the NPC open itself

        if (mode is "click" or "full")
        {
            if (await session.ClickAsync("Auctions Browser"))
            {
                actions++;
                await Task.Delay(TimeSpan.FromMilliseconds(800));
                var toggle = AuctionToggles[_auctionPasses % AuctionToggles.Length];
                if (await session.ClickAsync(toggle)) { actions++; await Task.Delay(TimeSpan.FromMilliseconds(700)); }
            }
        }

        if (mode is "sign" or "full")
        {
            // In sign mode this is the ONLY navigation done, so the pass is "open, reach the box, type".
            if (mode == "sign" && await session.ClickAsync("Auctions Browser"))
            {
                actions++;
                await Task.Delay(TimeSpan.FromMilliseconds(800));
            }

            if (await session.ClickAsync("Search"))
            {
                actions++;
                await session.SignAsync(AuctionSearches[_auctionPasses % AuctionSearches.Length]);
                actions++;
                await Task.Delay(TimeSpan.FromMilliseconds(900));
            }
        }

        // How the menu is closed is itself a variable.
        //
        // In the vanilla open/close capture the human closed by clicking the menu's own "Close" button, which
        // sends a Container Click; that capture contains ZERO Container Close packets. We always send
        // Container Close. A human pressing ESC would send one too, so this is not proof of anything — but it
        // is the one packet in our open/close cycle that the reference run never produced, and the cheapest
        // way to find out is to stop sending it and see whether time-to-kick changes.
        switch (mode)
        {
            case "button":
                // Close the way the reference capture did: click the menu's own Close entry.
                if (!await session.ClickAsync("Close")) await session.CloseAsync();
                break;

            case "dwell":
                // Hold the window open ~10s instead of the usual ~0.5s. Time-in-menu is otherwise a
                // constant across every arm (vanilla 0.72s per open, us 0.82s), so it has never been
                // varied. If the meter is fed by having a window open rather than by opening one,
                // this arm dies far sooner than `open` at the same open count.
                await Task.Delay(TimeSpan.FromSeconds(10));
                await session.CloseAsync();
                break;

            case "noclose":
                // Do not close at all. The next NPC open replaces the window server-side, which is legal and
                // isolates Container Close completely.
                break;

            default:
                await session.CloseAsync();
                break;
        }

        Say($"auction pass {_auctionPasses} [{mode}] done, {actions} menu action(s), " +
            $"{(DateTime.UtcNow - _stressStarted).TotalMinutes:F1}m clean");

        // Our open cadence is machine-regular to within ~10ms (measured gaps 20.42, 20.43, 20.43,
        // 20.43s), where a human's is ragged. "jitter" keeps everything else identical and only
        // randomises the spacing, so if regularity is what is being detected this arm survives.
        // Note the `approach` arm is just as regular and is NOT ejected, so regularity alone is not
        // the trigger — this tests regularity *of container opens* specifically.
        var gapSeconds = Math.Max(3, _options?.StressGapSeconds ?? 45);
        if (mode == "jitter") gapSeconds = _jitterGap.Next(8, 46);

        // "busy" spends the gap moving and looking around the way the human in the reference capture did,
        // instead of standing nearly inert. Everything else is identical to `open`.
        if (mode == "busy") await session.BusyIdleAsync(TimeSpan.FromSeconds(gapSeconds));
        else await session.IdleAsync(TimeSpan.FromSeconds(gapSeconds));
    }

    /// <summary>
    /// Joins one SPECIFIC hub by number through the Hub Selector NPC. Used to park an observation run
    /// somewhere a human can go and stand next to it; there is no chat command for this on Hypixel.
    /// </summary>
    private async Task<bool> GoToNamedHubAsync(int hubNumber)
    {
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20))) return false;
        if (!await session.WalkToAsync(HubSelectorPos, 120)) return false;
        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector")) return false;

        var wanted = $"SkyBlock Hub #{hubNumber}";
        var slot = session.FindSlot(wanted);
        if (slot is null)
        {
            Say($"\"{wanted}\" is not listed in \"{session.ContainerTitle}\"");
            await session.CloseAsync();
            return false;
        }

        var occupancy = OccupancyOf(slot);
        if (occupancy is { } o && o.Capacity > 0 && o.Players >= o.Capacity)
        {
            Say($"{wanted} is full ({o.Players}/{o.Capacity}) — joining it would bounce straight back out");
            await session.CloseAsync();
            return false;
        }

        _hub = slot.Name ?? wanted;
        _hubServer = ServerOf(slot) ?? "";
        Say($"taking {_hub}" + (occupancy is { } oc ? $" at {oc.Players}/{oc.Capacity}" : "") +
            $" (Server: {(_hubServer.Length > 0 ? _hubServer : "?")})");

        if (!await session.ClickAsync(slot.Name!)) return false;
        await Task.Delay(TimeSpan.FromSeconds(6));
        return true;
    }

    /// <summary>
    /// Hubs we have had to flee, and when. Without this the bot can hop straight back into the hub whose
    /// Bazaar the server just switched off — it is usually the busiest one, which is exactly why it is
    /// struggling, so "pick the busiest" walks right back into it.
    /// </summary>
    private readonly Dictionary<string, DateTime> _badHubs = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan BadHubCooldown = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Set when we had to accept a hub below <see cref="PortfolioOptions.MinHubPlayers"/> because nothing
    /// better was available. Null whenever we are somewhere that meets the threshold.
    /// </summary>
    private DateTime? _settledForQuietHubAt;

    private static readonly TimeSpan QuietHubRecheckAfter = TimeSpan.FromMinutes(15);

    private async Task<bool> GoToBusyHubAsync(PortfolioOptions options, bool mustSwitch = false)
    {
        if (!await session.FindNpcAsync("Hub Selector", TimeSpan.FromSeconds(20))) return false;
        if (!await session.WalkToAsync(HubSelectorPos, 120)) return false;
        await session.ApproachNpcAsync();
        if (!await session.OpenNpcMenuAsync("Hub Selector")) return false;

        var current = session.MenuSlots().FirstOrDefault(x =>
            x.Item.Contains("red_terracotta", StringComparison.OrdinalIgnoreCase));
        var here = current is null ? null : OccupancyOf(current);

        // In empty-hub mode the population test is inverted, so "busy enough to stay" never applies.
        if (!mustSwitch && !options.PreferEmptyHub && here is { } occupancy && occupancy.Players >= options.MinHubPlayers)
        {
            _hub = current?.Name ?? "current hub";
            _hubServer = current is null ? "" : ServerOf(current) ?? "";
            _settledForQuietHubAt = null;
            Say($"current hub holds {occupancy.Players}/{occupancy.Capacity} — busy enough");
            await session.CloseAsync();
            return true;
        }

        if (mustSwitch) Say("  must leave this server — switching hubs regardless of how busy this one is");

        // The hub we are being forced off is unusable for a while, not just right now: the Bazaar stays
        // disabled until the server recovers. Remember it so the "pick the busiest" rule below cannot send us
        // straight back — the broken hub is normally the busiest, which is why it is broken.
        var now = DateTime.UtcNow;
        if (mustSwitch && current?.Name is { } leaving) _badHubs[leaving] = now;
        foreach (var stale in _badHubs.Where(kv => now - kv.Value > BadHubCooldown).Select(kv => kv.Key).ToList())
            _badHubs.Remove(stale);

        // Every hub we could actually join: a full one cannot be entered however busy it looks.
        var joinable = session.MenuSlots()
            .Where(x => x.Name is not null && x.Name.Contains("SkyBlock Hub #", StringComparison.OrdinalIgnoreCase))
            .Where(x => current is null || x.Index != current.Index)
            .Where(x => !_badHubs.ContainsKey(x.Name!))
            .Select(x => (Slot: x, Occupancy: OccupancyOf(x)))
            .Where(x => x.Occupancy is { } o && (o.Capacity == 0 || o.Players < o.Capacity))
            .ToList();

        joinable = options.PreferEmptyHub
            ? joinable.OrderBy(x => x.Occupancy!.Value.Players).ToList()
            : joinable.OrderByDescending(x => x.Occupancy!.Value.Players).ToList();

        if (options.PreferEmptyHub)
        {
            var quietest = joinable.FirstOrDefault();
            if (quietest.Slot is null)
            {
                Say("no joinable hub at all — staying put");
                await session.CloseAsync();
                return true;
            }

            var q = quietest.Occupancy!.Value;
            Say($"empty-hub mode: taking {quietest.Slot.Name} at {q.Players}/{q.Capacity} " +
                $"(Server: {ServerOf(quietest.Slot) ?? "?"})");
            _hub = quietest.Slot.Name ?? "hub";
            _hubServer = ServerOf(quietest.Slot) ?? "";
            if (!await session.ClickAsync(quietest.Slot.Name!)) return false;
            await Task.Delay(TimeSpan.FromSeconds(6));
            return true;
        }

        var target = joinable.FirstOrDefault(x => x.Occupancy!.Value.Players >= options.MinHubPlayers);

        if (target.Slot is null)
        {
            // Nothing meets the threshold. Blending in is the point of the threshold, so take the busiest hub
            // that will actually let us in rather than staying somewhere emptier — the case where the only
            // genuinely busy hub is FULL used to leave the bot standing in whichever quiet one it started in.
            target = joinable.FirstOrDefault();

            if (target.Slot is null)
            {
                // Nowhere to go. When we are only here because this hub is nicer than the alternatives, that is
                // fine. When we are being forced off it, it is not: reporting success leaves the caller trading
                // against a Bazaar the server has switched off, which is the loop this whole path exists to
                // break. Fail instead, so the caller backs off and tries again once the menu has changed.
                if (mustSwitch)
                {
                    Say($"no joinable hub other than this one, and this one is unusable — giving up this attempt");
                    await session.CloseAsync();
                    return false;
                }

                Say($"no hub reaches {options.MinHubPlayers} players and none has room — staying put");
                await session.CloseAsync();
                return true;
            }

            var best = target.Occupancy!.Value;

            // Staying put is only ever right when this hub still WORKS. Under mustSwitch it is the one place we
            // cannot be, so a quieter hub beats the busy broken one — this is the case where the laggy hub is
            // also the only one above the threshold, and the old comparison pinned us to it for ever.
            if (!mustSwitch && here is { } mine && mine.Players >= best.Players)
            {
                Say($"no hub reaches {options.MinHubPlayers}; the busiest joinable holds {best.Players} " +
                    $"and we already have {mine.Players} — staying put");
                await session.CloseAsync();
                return true;
            }

            Say(mustSwitch
                ? $"no hub reaches {options.MinHubPlayers} and this one is unusable — taking the busiest joinable " +
                  $"at {best.Players}/{best.Capacity} and re-checking in {QuietHubRecheckAfter.TotalMinutes:F0} min"
                : $"no hub reaches {options.MinHubPlayers} — taking the busiest joinable one at {best.Players}/{best.Capacity}");

            // Settling for a quiet hub is a compromise, not a destination. Remember when, so the cycle can look
            // for something better once the hub list has had time to change.
            _settledForQuietHubAt = DateTime.UtcNow;
        }
        else
        {
            _settledForQuietHubAt = null;
        }

        Say($"moving to \"{target.Slot.Name}\" at {target.Occupancy!.Value.Players}/{target.Occupancy!.Value.Capacity} " +
            $"({ServerOf(target.Slot) ?? "server unknown"})");
        _hub = target.Slot.Name!;
        _hubServer = ServerOf(target.Slot) ?? "";
        session.ExpectRelocationFor(TimeSpan.FromSeconds(30));
        await session.ClickAsync(target.Slot.Name!);
        await Task.Delay(TimeSpan.FromSeconds(12));
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

        // Set before any of the work, not after: this used to be assigned only once the order menu had been
        // read, so a run whose cycles were failing to reach the NPC reported "cycle 1" indefinitely — the one
        // situation where someone watching the page most needs to see that cycles are passing.
        _state = $"cycle {cycle}";

        if (!await EnsureUsableHubAsync()) return;

        // Start every cycle with nothing open. A menu left idle times out on the server, and an interact sent
        // against a window the client still thinks is open goes nowhere.
        await session.CloseAsync();

        var orders = await ReadOrdersAsync();
        if (orders is null)
        {
            // Usually the NPC entity was replaced under us. Re-resolve and walk back before giving up on the
            // cycle; positions left unserviced are orders nobody is watching.
            Say($"cycle {cycle}: could not read the order manager — re-establishing at the Bazaar");
            await session.CloseAsync();
            if (await WalkToBazaarAsync()) orders = await ReadOrdersAsync();

            if (orders is null)
            {
                // Retrying in the same spot forever is not recovery. Observed 2026-08-08: the bot sat ~6
                // blocks from the Bazaar NPC from 01:00 to 02:10 — 56 failed interacts and 36 sidesteps —
                // because "re-establish at the Bazaar" walks back to the SAME npc on the SAME hub, and
                // whatever was blocking it (a player in the way, an entity that never resolves, terrain it
                // cannot close the last few blocks over) was still there. Seventy minutes of no trading and
                // no self-recovery. After a few consecutive failures, change hubs: a new server means a new
                // NPC instance, a new crowd and a fresh path to it.
                _orderManagerFailures++;
                Say($"cycle {cycle}: still no order manager " +
                    $"(consecutive failure {_orderManagerFailures}/{OrderManagerFailuresBeforeHubSwitch})");

                if (_orderManagerFailures >= OrderManagerFailuresBeforeHubSwitch)
                {
                    _orderManagerFailures = 0;
                    Say("  repeated failures at this Bazaar — warping out and switching hubs");
                    await session.CloseAsync();

                    // WARP first, do not walk.
                    //
                    // Switching hubs means walking to the Hub Selector, and if the reason we are failing is
                    // that we fell off the walkway, we cannot walk anywhere — observed stuck at y=61, eleven
                    // blocks below the Bazaar floor, where both the Bazaar walk AND the Hub Selector walk
                    // failed on repeat for half an hour. /hub is a teleport, so it works from anywhere,
                    // including the bottom of a hole.
                    await session.SendCommandAsync("hub");
                    await Task.Delay(TimeSpan.FromSeconds(12));

                    if (await GoToBusyHubAsync(_options, mustSwitch: true))
                        await WalkToBazaarAsync();
                }

                return;
            }
        }

        _orderManagerFailures = 0;

        Say($"cycle {cycle}: {_open.Count} open position(s), {orders.Count} order row(s), " +
            $"{Committed():N0}/{_options.Capital:N0} coins committed, realised {RealisedProfit():N1}");

        foreach (var position in _open.ToList())
        {
            await ServicePositionAsync(position, orders);

            // Saved per position rather than per cycle. A cycle that is interrupted part-way — which is what a
            // kick to the lobby does — otherwise loses every fill and close it had already made, and those are
            // exactly the facts that cannot be recovered from the order menu afterwards.
            PositionStore.Save(_open, _closed, Say);
        }

        _open.RemoveAll(p => p.Side == PositionSide.Closed);

        if (mayOpenNew) await OpenNewPositionsAsync();

        // Saved at the end of every cycle so a crash costs at most one cycle of ledger detail. Everything
        // above this line has already happened on the server whether or not the process survives.
        PositionStore.Save(_open, _closed, Say);
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

        if (await AbandonIfGoingNowhereAsync(position)) return;

        await ConsiderRepricingAsync(position, status);
    }

    /// <summary>
    /// How long a buy order may sit without a single unit filling before it is given up on.
    ///
    /// Only the BUY side can be abandoned. Once any goods are held the position is committed — cancelling a
    /// sell offer does not undo the purchase, it just leaves the items sitting in the inventory instead of on
    /// the market, so a stuck sell has to be worked out through repricing rather than dropped.
    /// </summary>
    private static readonly TimeSpan AbandonUnfilledBuyAfter = TimeSpan.FromHours(1);

    /// <summary>
    /// Cancels a buy order that has done nothing at all for an hour, freeing its escrow for a product that is
    /// actually moving. An order sitting unfilled is not harmless: its coins are locked, and with a capital
    /// cap that directly costs the trades that would otherwise have been opened.
    /// </summary>
    private async Task<bool> AbandonIfGoingNowhereAsync(Position position)
    {
        if (position.Side != PositionSide.Buying) return false;
        if (position.UnitsBought > 0) return false; // it has moved; that is not "nothing happened"

        var age = DateTime.UtcNow - position.Opened;
        if (age < AbandonUnfilledBuyAfter) return false;

        Say($"  {position.OrderName}: nothing filled in {age.TotalMinutes:N0} minutes — cancelling to free the capital");

        if (!await CancelAsync(position))
        {
            Say($"  {position.OrderName}: cancel failed; will try again next cycle");
            return true;
        }

        position.OrderLive = false;
        position.Side = PositionSide.Closed;
        position.ClosedAt = DateTime.UtcNow;
        _closed.Add(position);

        // Recorded against the product so a market that never fills stops being chosen. Zero profit over the
        // time its capital was tied up is exactly the signal the scorecard exists to act on.
        _scorecard.RecordOutcome(position.ProductKey, position.Name, 0, age, DateTime.UtcNow);
        _scorecard.Save(Say);
        return true;
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
                position.ClosedAt = DateTime.UtcNow;
                _closed.Add(position);
                return;
            }

            position.Side = PositionSide.Selling;
            position.LegStarted = DateTime.UtcNow;
            position.Steps = 0;
            position.PollsBeaten = 0;

            // A failed placement leaves the position on the sell leg with no order on the book. That is a
            // state to retry, not a finished flip — and it used to become one, because the result was ignored.
            if (!await PlaceSellAsync(position))
            {
                position.OrderLive = false;
                Say($"  {position.Name}: could not place the sell offer — holding {position.UnitsBought} and retrying");
            }
            return;
        }

        if (position.Side == PositionSide.Selling)
        {
            // Goods still held means the flip is not over, whatever the order menu shows. A missing row here
            // means our offer is not up — which is a reason to put one up, not to write the stock off. This
            // exact case booked a phantom 133,355 loss on five Hummingbird Shards we still owned, and then
            // benched a profitable product on the strength of it.
            if (position.UnitsSold < position.UnitsBought)
            {
                Say($"  {position.Name}: no sell offer on the book but {position.UnitsBought - position.UnitsSold} " +
                    "still held — re-listing rather than closing");

                position.LegStarted = DateTime.UtcNow;
                if (!await PlaceSellAsync(position)) position.OrderLive = false;
                return;
            }

            position.Side = PositionSide.Closed;
            _closed.Add(position);
            Say($"  {position.Name}: CLOSED — bought {position.UnitsBought} for {position.CoinsSpent:N1}, " +
                $"sold {position.UnitsSold} for {position.CoinsReceived:N1}, profit {position.Profit:N1}");

            // Only measured flips teach us anything: an invented cost basis would record a fictional profit
            // and then steer future selection with it.
            if (position.BasisKnown)
            {
                _scorecard.RecordOutcome(position.ProductKey, position.Name, position.Profit,
                    DateTime.UtcNow - position.Opened, DateTime.UtcNow);
                _scorecard.Save(Say);
            }
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
            // Zero when the cost was never measured. The policy protects margin ABOVE cost, so feeding it an
            // invented basis makes every possible step score negative and the order holds forever — an
            // Enchanted Slimeball offer sat above the market for over an hour on exactly this. Zero says
            // "there is no margin to defend here", which lets it follow the book out. This is a SECOND floor,
            // distinct from the one in PlaceSellAsync; fixing that one alone did not help because a position
            // stuck on this decision never reaches it.
            CostPerUnit: position.BasisKnown ? position.CostPerUnit : 0,
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
                // Move on to the next candidate instead of ending the cycle. Giving up here meant one
                // unusable product at the top of the list stalled every cycle for the whole session.
                Say($"  could not open a position in {flip.Name} — trying the next candidate");
                _unusable.Add(flip.ProductKey);
                continue;
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

        var now = DateTime.UtcNow;

        // Never double up on a product already in flight: two orders from the same account at the same price
        // level compete with each other, and a re-price on one would be reacting to the other.
        var eligible = flips.Where(f =>
            !inFlight.Contains(f.ProductKey) &&
            !_unusable.Contains(f.ProductKey) &&
            !f.IsManipulated &&
            !f.Unstackable &&
            f.EstimatedProfitPerUnit > 0 &&
            f.BestAskPrice <= _options.MaxUnitPrice).ToList();

        // Products that have repeatedly failed to earn are dropped, but only once their record has decayed to
        // nothing does the ban lift — a market that has genuinely turned around gets another chance.
        var benched = eligible.Where(f => _scorecard.IsBenched(f.ProductKey, now)).ToList();
        foreach (var f in benched) Say($"  skipping {f.Name} — its recent trades have not paid ({_scorecard.ScoreOf(f.ProductKey, now):N0}/h)");
        eligible = eligible.Except(benched).ToList();

        // Proven products are pulled in even when the API has stopped listing them.
        //
        // Without this the scorecard can only REORDER the API's forty suggestions, never add to them — so the
        // moment a winner drops out of that list its record becomes decorative. Chuckwalla Shard was the best
        // trade of the night and sat unpicked at the top of the preferences table for exactly this reason.
        // A product that has actually paid deserves to be looked at on its own account.
        eligible.AddRange(await ProvenCandidatesAsync(eligible, inFlight, now, budget));

        if (eligible.Count == 0)
        {
            Say("  no candidate left after excluding what we already hold");
            return null;
        }

        // The API ranks on the current book, which predicts what a flip should earn. Its own past performance
        // is what it DID earn, so the two are combined rather than either being trusted alone: the API keeps
        // the bot exploring, the scorecard makes it lean into whatever is actually working.
        var candidate = eligible
            .OrderByDescending(f => _scorecard.ScoreOf(f.ProductKey, now))
            .ThenBy(f => eligible.IndexOf(f))
            .First();

        var score = _scorecard.ScoreOf(candidate.ProductKey, now);
        if (score > 0) Say($"  favouring {candidate.Name} on its record ({score:N0} coins/h, decayed)");

        return candidate;
    }

    /// <summary>
    /// Builds candidates from the bot's own track record, for products the API is not currently suggesting.
    ///
    /// Each one is re-checked against its live book before being offered: a good record is a reason to LOOK
    /// at a product, never a reason to trade it at a price that no longer works. A product whose spread has
    /// closed is skipped this cycle and will be picked up again if it reopens.
    ///
    /// <paramref name="budget"/> matters because the API is asked for flips affordable at that budget, so a
    /// product can be missing from its list purely for being too expensive right now. Re-adding those without
    /// re-checking the budget proposed positions the bot could not fund — with the capital fully committed the
    /// budget goes negative, and Jungle Key at 169,631 was still being reconsidered every cycle, costing a
    /// live book lookup each time and reporting "the API is not listing it" as though the product had been
    /// dropped on merit.
    /// </summary>
    private async Task<List<FlipCandidate>> ProvenCandidatesAsync(
        List<FlipCandidate> already, HashSet<string> inFlight, DateTime now, double budget)
    {
        // Nothing is affordable, so nothing is worth looking up.
        if (budget <= 0) return [];

        var have = already.Select(f => f.ProductKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<FlipCandidate>();

        var proven = _scorecard.Top(now, 6)
            .Where(t => t.Score > 0
                        && !have.Contains(t.Key)
                        && !inFlight.Contains(t.Key)
                        && !_unusable.Contains(t.Key)
                        && !t.Key.Contains(' ')            // a display name where a key belongs: unusable here
                        && !_scorecard.IsBenched(t.Key, now))
            .ToList();

        foreach (var (key, name, score, _, _) in proven)
        {
            var book = await LiveBookAsync(key);
            if (book is null || book.BidPrice <= 0 || book.AskPrice <= 0) continue;

            // The same test the API applies: what a unit would earn after tax if bought at the bid and sold
            // at the ask, one tick inside each.
            var buy = Math.Round(book.BidPrice + RepricePolicy.Tick, 1);
            var sell = Math.Round(book.AskPrice - RepricePolicy.Tick, 1);
            var margin = sell * (1 - TaxRate) - buy;

            if (margin <= 0)
            {
                Say($"  {name} has a record ({score:N0}/h) but its spread no longer pays ({margin:N1}/unit) — skipping");
                continue;
            }

            if (buy > _options.MaxUnitPrice) continue;

            // A single unit has to fit the budget, or this is a suggestion the bot cannot act on.
            if (buy > budget) continue;

            // Deliberately does not claim WHY the API omitted it: the flips request filters on budget, price,
            // fill time and manipulation all at once, and this code checked none of them before it asserted a
            // reason.
            Say($"  reconsidering {name} on its record ({score:N0}/h) — not in the API's current list, spread pays {margin:N1}/unit");

            found.Add(new FlipCandidate(
                ProductKey: key,
                Name: name,
                BestBidPrice: book.BidPrice,
                BestAskPrice: book.AskPrice,
                EstimatedProfitPerUnit: margin,
                EstimatedRoundTripMinutes: 0,
                SuggestedQuantity: 0,
                IsManipulated: false,
                Unstackable: false));
        }

        return found;
    }

    private async Task<bool> PlaceBuyAsync(Position position, double? price)
    {
        var resolved = await OpenProductAsync(position.Name);
        if (resolved is null) return false;
        position.Name = resolved;

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
        var resolved = await OpenProductAsync(position.Name);
        if (resolved is null) return false;
        position.Name = resolved;

        if (session.BestAsk is not { } ask) return false;

        var sellPrice = price ?? Math.Round(ask - RepricePolicy.Tick, 1);

        // Never below cost: the floor is the price whose post-tax proceeds still clear what we paid.
        //
        // Only when the cost is MEASURED. A position adopted from the order menu has an invented basis — the
        // offer price it happened to be sitting at — and defending that fiction makes the position unsellable
        // for good: the floor lands just above its own offer, so every reprice pushes the price further from
        // the market. That is exactly what stranded an Enchanted Slimeball offer for over an hour. With no
        // real cost to protect there is nothing to protect, so follow the book.
        if (position.CostPerUnit > 0 && position.BasisKnown)
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
        if (await OpenProductAsync(position.Name) is null) return false;

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

    /// <summary>Opens a product page, returning the name the GAME uses for it, or null if it cannot be found.</summary>
    private async Task<string?> OpenProductAsync(string productName)
    {
        // The API's display name is not always what the Bazaar's search box expects — "Shard Foxtrot" comes
        // back as a grid of "No Product Found" — so the obvious rewrites are tried before the product is
        // written off. Cheap: a miss costs one search, and the alternative is discarding a tradable product.
        foreach (var term in SearchTermsFor(productName))
        {
            if (!await EnsureUsableHubAsync()) return null;
            if (!await session.OpenNpcMenuAsync("Bazaar")) return null;
            if (!await session.ClickAsync("Search")) return null;
            if (!await session.SignAsync(term)) return null;

            // "No Product Found" fills the grid when the search matched nothing; there is no point clicking.
            var match = MatchProduct(session.MenuSlots(), productName, term);
            if (match?.Name is null)
            {
                Say($"  search for \"{term}\" found nothing");
                await session.CloseAsync();
                continue;
            }

            if (!await session.ClickAsync(match.Name))
            {
                await session.CloseAsync();
                continue;
            }

            if (IsProductPage(session.ContainerTitle, match.Name))
            {
                if (!string.Equals(match.Name, productName, StringComparison.OrdinalIgnoreCase))
                    Say($"  \"{productName}\" is \"{match.Name}\" in game — using the game's name from here on");
                return match.Name;
            }

            Say($"  expected the {match.Name} page, got \"{session.ContainerTitle}\"");
            await session.CloseAsync();
        }

        return null;
    }

    /// <summary>
    /// Characters a real player could type into a sign line.
    ///
    /// Vanilla limits sign input by RENDERED WIDTH — <c>SignBlockEntity.MAX_TEXT_LINE_WIDTH = 90</c> px — not
    /// by character count, which works out around fifteen characters of ordinary text. The protocol itself
    /// accepts 384, so a long search term is silently accepted by the server while being something no client
    /// could have produced. Terms are kept inside the limit for that reason; the Bazaar's search matches on
    /// substrings, so a shortened term still finds the product and <see cref="MatchProduct"/> picks it out.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/entity/SignBlockEntity.java:39
    /// </summary>
    private const int SignLineLimit = 15;

    /// <summary>Trims a term to something a player could actually type, preferring a whole-word boundary.</summary>
    private static string FitToSign(string term)
    {
        if (term.Length <= SignLineLimit) return term;

        var cut = term[..SignLineLimit];
        var lastSpace = cut.LastIndexOf(' ');

        // A word boundary keeps the term meaningful; mid-word is fine if the first word is itself too long.
        return lastSpace >= 4 ? cut[..lastSpace] : cut;
    }

    /// <summary>
    /// Decides whether the open container really is this product's page.
    ///
    /// Container titles are truncated (~30 chars) and Hypixel prefixes them with a category breadcrumb, so
    /// "Suspicious Scrap" arrives as "Glacite Tunnels ➜ Suspicious Sc" — a containment test can never match
    /// it, and the bot was silently rejecting every product whose name did not survive truncation. The tail
    /// after the breadcrumb is compared as a PREFIX in either direction instead.
    /// </summary>
    private static bool IsProductPage(string title, string productName)
    {
        var tail = title.Contains('➜') ? title[(title.LastIndexOf('➜') + 1)..] : title;
        tail = tail.Trim();

        // The search RESULTS page is titled Bazaar ➜ "term" — quoted, and never a product page. Without this
        // a search for "Suspicious" would accept the results grid as the Suspicious Scrap page.
        if (tail.StartsWith('"') && tail.EndsWith('"')) return false;
        if (tail.Length < 4) return false;

        return productName.StartsWith(tail, StringComparison.OrdinalIgnoreCase)
               || tail.StartsWith(productName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Search terms to try for a product the API calls <paramref name="name"/>, best guess first.
    ///
    /// Most products are named identically in both places, but the programmatic families — shards, gems,
    /// enchantments — come back from the API with the words the other way round ("Shard Foxtrot" for what the
    /// game lists as "Foxtrot Shard"). It is not a reliable rule, so rather than maintaining a list of
    /// prefixes this rotates the first word to the end (which covers every one of those families) and finally
    /// falls back to searching the single most distinctive word, letting the token match below sort out which
    /// result is really the product.
    /// </summary>
    private static IEnumerable<string> SearchTermsFor(string name)
    {
        // Every term is trimmed to what a player could type; duplicates are dropped because trimming can
        // collapse two candidates onto the same string.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> Candidates()
        {
            yield return name;

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) yield break;

            yield return string.Join(' ', words.Skip(1).Append(words[0]));

            // The longest word is the one least likely to be a family label like "Shard" or a numeral.
            yield return words.OrderByDescending(w => w.Length).First();
        }

        foreach (var candidate in Candidates())
        {
            var term = FitToSign(candidate);
            if (term.Length > 0 && seen.Add(term)) yield return term;
        }
    }

    /// <summary>
    /// Picks the search result that IS this product, or null rather than a guess.
    ///
    /// Matching on the whole term fails as soon as the game orders the words differently, so the test that
    /// decides is whether a result contains every word of the name in any order — true of "Foxtrot Shard" for
    /// "Shard Foxtrot", false for a different shard that merely shares the family word.
    ///
    /// There is deliberately NO looser fallback. One used to exist ("first result containing the search
    /// term") and it spent real coins on the wrong item: a search for Enchanted Titanium fell back to the
    /// trimmed term "Enchanted" and bought six Enchanted Acacia Log instead. When the product cannot be
    /// identified with certainty the only safe answer is to identify nothing and let the caller move on.
    /// </summary>
    private static MenuSlot? MatchProduct(IEnumerable<MenuSlot> results, string apiName, string term)
    {
        var candidates = results
            .Where(r => r.Name is not null && !r.Name.Contains("No Product Found", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var words = apiName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return candidates.FirstOrDefault(r => string.Equals(r.Name, apiName, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault(r => words.All(w => r.Name!.Contains(w, StringComparison.OrdinalIgnoreCase)));
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

    private string _state = "starting";

    /// <summary>
    /// Builds the status snapshot on demand. Pure reporting — it decides nothing and mutates nothing.
    ///
    /// Called from the web server's thread while the trading loop mutates these lists, so both are copied
    /// defensively: a page refresh must never be able to disturb, or be disturbed by, a trade.
    /// </summary>
    private object BuildStatusSnapshot()
    {
        var self = session.Client.State.LocalPlayer?.Entity?.Position;
        var open = _open.ToArray();
        var closed = _closed.ToArray();

        return new
        {
            state = _state,
            hub = _hub,
            server = _hubServer,
            position = self is null ? "" : $"({self.X:F0}, {self.Y:F0}, {self.Z:F0})",
            connected = session.Client.IsConnected,
            intercepted = session.Intercepted,
            capital = _options.Capital,

            // Read live from the scoreboard sidebar, which is the only place SkyBlock publishes it. Null when
            // the sidebar has no purse line (a lobby, or before it first arrives) — shown as unknown rather
            // than zero, because zero would read as "broke" when it means "not yet seen".
            purse = session.Client.State.Level.Sidebar.ReadPurse(session.Client.State.Level.Teams),
            committed = open.Sum(p => p.Committed),
            realisedProfit = closed.Where(p => p.BasisKnown).Sum(p => p.Profit),

            // Earning RATE, measured over the span the profit was actually earned in — from the oldest closed
            // position's opening to now — not over this process's uptime. Realised P&L carries across
            // restarts, and the bot has restarted many times tonight, so dividing by session uptime would
            // report a rate several times the truth.
            realisedPerHour = RealisedPerHour(closed),
            closedCount = closed.Length,
            profitableCount = closed.Count(p => p.BasisKnown && p.Profit > 0),
            runningMinutes = (DateTime.UtcNow - _startedUtc).TotalMinutes,

            // Time on the server since the last ejection, as opposed to process uptime. These have to be
            // emitted HERE, not just on StatusServer: once a SnapshotProvider is set the page renders that
            // object and ignores StatusServer's own payload, so fields added only there silently read as
            // undefined (which is why "clean" showed 0m against a 6m runtime).
            cleanMinutes = status is null ? 0 : (DateTime.UtcNow - status.SessionStartedUtc).TotalMinutes,
            ejectionCount = status?.EjectionCount ?? 0,
            bestSessionMinutes = status?.BestSessionMinutes ?? 0,

            open = open.Select(p => new
            {
                name = p.Name,
                side = p.Side.ToString(),
                quantity = p.Quantity,
                price = p.OrderPrice,
                unitsBought = p.UnitsBought,
                spent = p.CoinsSpent,
                ageMinutes = (DateTime.UtcNow - p.LegStarted).TotalMinutes,
                steps = p.Steps
            }).ToList(),
            closed = closed
                // Most recent first: the interesting flip is the one that just happened, and the card only
                // shows a handful without scrolling.
                //
                // The tie-break carries the weight. Positions closed before ClosedAt existed are rehydrated
                // from the state file with a null forever, so ordering on the timestamp alone left all of
                // them tied and a stable sort kept them in INSERTION order — oldest at the top, newest buried
                // at the bottom, which is the exact complaint the sort was added to fix. _closed is append-
                // ordered, so falling back to a descending index restores newest-first for the unstamped ones.
                .Select((p, index) => (Position: p, Index: index))
                .OrderByDescending(x => x.Position.ClosedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.Index)
                .Select(x => x.Position)
                .Select(p => new
                {
                    name = p.Name,
                    unitsBought = p.UnitsBought,
                    spent = p.CoinsSpent,
                    unitsSold = p.UnitsSold,
                    received = p.CoinsReceived,
                    profit = p.Profit,
                    basisKnown = p.BasisKnown,

                    closedAt = p.ClosedAt,
                    closedAgoMinutes = p.ClosedAt is { } t ? (DateTime.UtcNow - t).TotalMinutes : (double?)null,

                    // ClosedAt post-dates most of the ledger, so the exact figure above is null for every
                    // flip recorded before it existed — which rendered the whole column as em-dashes and
                    // made it look broken. The flip's own start IS known for those, and "closed some time
                    // after it opened" is true and useful, so it is offered as a clearly-marked fallback
                    // (the page prefixes it with ~) rather than being silently passed off as the real time.
                    openedAgoMinutes = p.ClosedAt is null && (p.Opened != default ? p.Opened : p.LegStarted) is var o
                                       && o != default
                        ? (DateTime.UtcNow - o).TotalMinutes
                        : (double?)null
                }).ToList(),

            lastCloseAgoMinutes = closed
                .Where(p => p.ClosedAt is not null)
                .Select(p => (double?)(DateTime.UtcNow - p.ClosedAt!.Value).TotalMinutes)
                .DefaultIfEmpty(null)
                .Min(),

            pnlSeries = BuildPnlSeries(closed),

            // What the bot has learned to prefer, decayed to this instant — so the page shows the score that
            // is actually steering the next choice, not the raw figure recorded when the flip closed.
            preferences = _scorecard.Top(DateTime.UtcNow, 12).Select(t => new
            {
                name = t.Name,
                score = t.Score,
                trades = t.Trades,
                total = t.Total,
                benched = _scorecard.IsBenched(t.Key, DateTime.UtcNow)
            }).ToList()
        };
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

        // Positions inherited without a saved cost are excluded: their "profit" is measured against an assumed
        // basis, so folding them in would report a number the session did not actually earn.
        var measured = _closed.Where(p => p.BasisKnown).ToList();
        var inherited = _closed.Count - measured.Count;

        var realised = measured.Sum(p => p.Profit);
        var winners = measured.Count(p => p.Profit > 0);
        Say($"  realised P&L: {realised:N1} coins across {measured.Count} closed ({winners} profitable)");
        if (inherited > 0)
            Say($"  ({inherited} inherited position(s) closed with no known cost basis, excluded from that figure)");
        Say("=========================================================");
    }

    // ===== Shared plumbing =====

    private async Task<bool> EnsureUsableHubAsync()
    {
        if (session.Intercepted) return false;

        // Checked before anything else, because in this state every other check is reading a world we are no
        // longer in. Hypixel rejects a warp sent immediately after dropping us, so the pause is not politeness.
        if (session.LobbyEjection is { } ejection)
        {
            Say($"ejected to the lobby ({ejection}) — waiting, then warping back");
            session.StopMoving();

            // Adaptive back-off: rejoining instantly feeds whatever is kicking us.
            //
            // Measured 2026-08-08, one session, intervals between ejections:
            //     7.8 -> 5.0 -> 2.1 -> 1.8 -> 1.8 -> 1.7 min
            // Monotonic collapse to a ~1.7 min floor. After the bot was stopped for ~28 minutes the next
            // session's first ejection came 10 minutes in, i.e. back to baseline — so the penalty decays with
            // time OFF the server. That is a leaky bucket, and reconnecting immediately keeps it full.
            // Waiting longer when the interval collapses should raise total trading time, not lower it: two
            // minutes of work per rejoin is worse than eight minutes of work after a pause.
            var now = DateTime.UtcNow;
            _recentEjections.Add(now);
            _recentEjections.RemoveAll(t => now - t > TimeSpan.FromMinutes(30));

            // Bank the clean stay that just ended. Time-since-last-ejection is the number the whole ejection
            // investigation is judged on, so it has to be visible rather than inferred from the log.
            status?.NoteEjection();

            var backoff = TimeSpan.FromSeconds(10);
            if (_recentEjections.Count >= 2)
            {
                var gap = _recentEjections[^1] - _recentEjections[^2];
                if (gap < TimeSpan.FromMinutes(3))
                {
                    // Escalate with how many ejections are stacked up, capped so the bot never parks for ever.
                    var minutes = Math.Min(10, 2 * (_recentEjections.Count - 1));
                    backoff = TimeSpan.FromMinutes(minutes);
                    Say($"  ejections are accelerating ({gap.TotalMinutes:F1} min since the last, " +
                        $"{_recentEjections.Count} in the past 30) — backing off {minutes} min before rejoining");
                }
            }

            await Task.Delay(backoff);

            await session.SendCommandAsync("skyblock");
            await Task.Delay(TimeSpan.FromSeconds(9));
            await session.SendCommandAsync("hub");
            await Task.Delay(TimeSpan.FromSeconds(12));

            session.ClearLobbyEjection();
            session.ClearRestartState();
            session.ClearOutage();
            await session.SelectEmptyHotbarSlotAsync();

            // Re-picks the hub by population as if joining fresh: the lobby drops us wherever it likes.
            return await GoToBusyHubAsync(_options) && await WalkToBazaarAsync();
        }

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
            return await GoToBusyHubAsync(_options, mustSwitch: true) && await WalkToBazaarAsync();
        }

        // Nothing is wrong — but we may be sitting on a hub we only accepted because everything better was
        // full, broken, or on cooldown. Hub populations swing over tens of minutes, so look again periodically
        // rather than serving out the session in a hub too quiet to blend into.
        //
        // The re-check itself opens the Hub Selector, and container opens are what get us ejected, so it is
        // deliberately infrequent (~4/hour at the default) and only runs while the compromise is in force.
        if (_settledForQuietHubAt is { } since && DateTime.UtcNow - since >= QuietHubRecheckAfter)
        {
            Say($"settled for a quiet hub {(DateTime.UtcNow - since).TotalMinutes:F0} min ago — checking for a better one");

            // Cleared first so a re-check that finds nothing better restarts the clock instead of retrying every
            // cycle. GoToBusyHubAsync sets it again if it has to compromise a second time.
            _settledForQuietHubAt = null;

            if (!await GoToBusyHubAsync(_options)) return false;
            return await WalkToBazaarAsync();
        }

        return true;
    }

    /// <summary>
    /// Fetches the live book for a product, or null if the API cannot serve it.
    ///
    /// A null means the repricing policy simply holds this poll, which is the safe direction: it can leave an
    /// order sitting one tick behind the book, but it can never move a price on bad data.
    /// </summary>
    private async Task<LiveProduct?> LiveBookAsync(string productKey)
    {
        // A position rebuilt from the order menu carries the DISPLAY name where a key belongs, with no way to
        // recover the real one. Product keys are underscore-cased and never contain a space, so this tells the
        // two apart without asking the server 404 after 404. Said once per product, not once per poll.
        if (productKey.Contains(' '))
        {
            if (_keylessProducts.Add(productKey))
                Say($"  \"{productKey}\" has no API product key — it will be worked to a close without repricing");
            return null;
        }

        try
        {
            return await api.GetFromJsonAsync<LiveProduct>($"/api/bot/products/{productKey}");
        }
        catch (Exception ex)
        {
            Say($"  live book lookup failed for {productKey} ({ex.Message})");
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
